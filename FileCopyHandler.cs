using System.IO;
using MessageBox = System.Windows.Forms.MessageBox;

namespace Vega;

public class FileCopyHandler
{
    private readonly System.Windows.Controls.ProgressBar _progressBar;
    private readonly string _login; // User login
    private readonly string _rawPath; // Path to the raw folder
    private readonly string _workPath; // Path to the work folder
    private long _completedSize; //Copied size
    private readonly long _totalSize; // Total size of the files to copy
    private readonly SemaphoreSlim _semaphore; // Semaphore to limit the number of concurrent tasks
    private readonly MainWindow _mainWindow; // Reference to the main window
    private readonly int _bufferSize; // Buffer size for file copy
    private CancellationTokenSource _cts = new();
    private bool _isPaused = false;
    private readonly object _pauseLock = new();


    private readonly HashSet<string>
        _archiveExtensions = [".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz"]; // Archive formats


    public FileCopyHandler(string remoteProjectPath, string workPath, string rawPath,
        System.Windows.Controls.ProgressBar progressBar,
        string login, long totalSize, MainWindow mainWindow, int customBufferSize,
        int maxConcurrency = 4)
    {
        _mainWindow = mainWindow;
        _mainWindow.RemoteProjectPath = remoteProjectPath;
        this._workPath = workPath;
        this._rawPath = rawPath;
        _progressBar = progressBar;
        _login = login;
        _mainWindow.Login = login;
        _totalSize = totalSize;
        _semaphore = new SemaphoreSlim(maxConcurrency);
        _bufferSize = customBufferSize;
    }

    // Copy files in a multithreaded way
    public async Task CopyFilesMultithreaded(IEnumerable<Dir> selectedItems)
    {
        var tasks = new List<Task>(); // List of tasks to wait for

        foreach (Dir selected in selectedItems)
        {
            await _semaphore.WaitAsync(); // Wait for a semaphore slot to be available
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    CopyFileOrDirectory(selected); // Copy the file or directory
                }
                finally
                {
                    _semaphore.Release(); // Release the semaphore slot
                }
            }));
        }

        try
        {
            // Wait for all tasks to complete
            await Task.WhenAll(tasks);
            _mainWindow.ProgressStatus.Content = "All files copied successfully.";
        }
        // Catch exceptions from the tasks
        catch (AggregateException ex)
        {
            _mainWindow.LockUi(false);
            MessageBox.Show($"Errors occurred during file copying: {ex.Flatten().Message}", "ERROR",  MessageBoxButtons.OK, MessageBoxIcon.Error);
            _mainWindow.LoggingEvent(ex.Message, _login, ex.StackTrace ?? string.Empty);
        }
    }

    private void CopyFileOrDirectory(Dir selected)
    {
        string sourcePath = Path.Combine(_mainWindow.RemoteProjectPath!, selected.Name);
        string destinationPath = Path.Combine(selected.Name.StartsWith('0') ? _rawPath : _workPath, selected.Name);

        try
        {
            if (File.Exists(sourcePath) && IsCompressedFile(sourcePath))
            {
                MoveCompressedFile(sourcePath, destinationPath);
            }
            else if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, destinationPath);
            }
            else if (File.Exists(sourcePath))
            {
                // Verify if the file is an archive
                CopyFileInChunks(sourcePath, destinationPath);
            }
            else
            {
                throw new Exception($"{sourcePath} doesn't exist");
            }

            Interlocked.Add(ref _completedSize, selected.Size);
            UpdateProgress();
        }
        catch (Exception ex)
        {
            _mainWindow.LockUi(true);
            MessageBox.Show($"Error occurred during copying {sourcePath}: {ex.Message}", "ERROR",  MessageBoxButtons.OK, MessageBoxIcon.Error);
            _mainWindow.LoggingEvent(ex.Message, _login, ex.StackTrace ?? string.Empty);
            throw;
        }
    }

    private bool IsCompressedFile(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        return _archiveExtensions.Contains(extension);
    }

    private void MoveCompressedFile(string sourceFile, string destinationFile)
    {
        try
        {
            // Déplacer l'archive sans toucher à son contenu
            File.Move(sourceFile, destinationFile, true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error copying compressed file {sourceFile}: {ex.Message}", "ERROR",  MessageBoxButtons.OK, MessageBoxIcon.Error);
            _mainWindow.LoggingEvent(ex.Message, _login, ex.StackTrace ?? string.Empty);
        }
        UpdateProgress();
    }

    private void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        // Copy all files in the directory
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            CopyFileInChunks(file, destFile);
        }

        // If the directory has subdirectories, copy them recursively
        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string destDir = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }

    private void CopyFileInChunks(string sourceFile, string destinationFile)
    {
        // Create the destination directory if it doesn't exist
        using (var sourceStream =
               new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize))
            // Open the source file
        using (var destinationStream =
               new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, _bufferSize))
        {
            // Copy the file in chunks
            byte[] buffer = new byte[_bufferSize];
            int bytesRead;
            // Read the source file in chunks
            while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                lock (_pauseLock)
                {
                    while (_isPaused)
                    {
                        Monitor.Wait(_pauseLock); // Attend que Resume() soit appelé
                    }
                }
                // Write the chunk to the destination file
                destinationStream.Write(buffer, 0, bytesRead);

                // Update _completedSize
                Interlocked.Add(ref _completedSize, bytesRead);

                // Update the progress bar for each chunk
                UpdateProgress();
            }

            if (_cts.Token.IsCancellationRequested)
            {
                return; // Sort de la fonction
            }
        }
    }

    private void UpdateProgress()
    {
        // Calculate the permillage of the copied size
        var permillage = (int)((double)_completedSize / _totalSize * 1000);

        // Updating the progress bar on the UI thread
        _progressBar.Dispatcher.Invoke(() => _progressBar.Value = permillage);
    }

    public void Pause()
    {
        lock (_pauseLock)
        {
            _isPaused = true; // Pause all threads
        }
    }

    public void Resume()
    {
        lock (_pauseLock)
        {
            _isPaused = false;
            Monitor.PulseAll(_pauseLock); // Resume all threads waiting on _pauseLock
        }

    }
}