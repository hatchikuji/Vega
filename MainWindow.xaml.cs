using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Directory = System.IO.Directory;
using Exception = System.Exception;
using File = System.IO.File;
using Label = System.Windows.Controls.Label;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using ProgressBar = System.Windows.Controls.ProgressBar;
using TextBox = System.Windows.Controls.TextBox;

namespace Vega;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        this.Initialized += VeryfyVeracrypt;
        this.Closed += Window_Closed;
        // Set the color of the progress bar because the default color is ugly
    }


    #region Variables

    // Variables to modifie the differents data
    private static double
        _rawLowerSizeFactor =
            1.09; // Factor to increase the size of the RAW DATA containers if the size is more than 50MB

    private const double
        RawUpperSizeFactor =
            1.5; // Factor to increase the size of the RAW DATA containers if the size is less than 50MB

    private static double
        _workLowerSizeFactor =
            1.09; // Factor to increase the size of the WORK DATA containers if the size is more than 50MB

    private const double
        WorkUpperSizeFactor =
            1.5; // Factor to increase the size of the WORK DATA containers if the size is less than 50MB

    private const int PasswordLength = 25; // Length of the password

    private const int Buffersize = 262144; // Buffer size for file copy

    private long _totalSize; // Size of the data transfered

    // Variables to store the different paths and used in veracrypt command line
    private string _workMapping = "";
    private string _workLowerLetter = "";
    private string _rawMapping = "";
    private string _rawLowerLetter = "";

    // Color for the progress bar
    private readonly SolidColorBrush _windowsOrange = new(Color.FromArgb(255,244,180,0));
    private readonly SolidColorBrush _windowsGreen = new(Color.FromArgb(255, 51, 204, 51));


    // Filepaths to the VeraCrypt executables
    private static readonly string VeraCryptPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VeraCrypt", "VeraCrypt.exe");

    private static readonly string VeraCryptFormatPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VeraCrypt",
            "VeraCrypt Format.exe"); // Filepath to the VeraCrypt Format executable

    private static string
        _localProjectFolderPath =
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); // Path to the local project folder

    private static readonly string
        Loggingfolderpath = Directory.GetCurrentDirectory(); // Path to the logging folder

    private string? _computerName = "";

    public string? RemoteProjectPath = "";

    public string? Login = "";

    private string? _password = "";

    private NetworkShareAccesser _connection;

    private FileCopyHandler _fileCopyHandler;

    #endregion

    // Used to identify NetworkShareAccesser errors and Win32 errors
    private static string GetSystemMessage(uint errorCode)
    {
        var exception = new Win32Exception((int)errorCode);
        return exception.Message;
    }

    // Verify if the VeraCrypt executables are present
    static void VeryfyVeracrypt(object? sender, EventArgs eventArgs)
    {
        if (!File.Exists(VeraCryptPath) || !File.Exists(VeraCryptFormatPath))
        {
            MessageBox.Show(
                "VeraCrypt executables not found please install it at https://www.veracrypt.fr/en/Downloads.html or reset its path",
                "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            throw new FileNotFoundException("VeraCrypt executables not found");
        }
    }

    // Action on windows closed
    private void Window_Closed(object? sender, EventArgs eventArgs)
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        if (allDrives.Any(drive => drive.Name == _workMapping))
        {
            ProcessStartInfo workUnloadInfo = new()
            {
                FileName = VeraCryptPath,
                Arguments = $"/quit /dismount {_workLowerLetter}"
            };
            Process workUnloadProcess = Process.Start(workUnloadInfo) ?? throw new InvalidOperationException();
            workUnloadProcess.WaitForExit();
        }

        if (allDrives.Any(drive => drive.Name == _rawMapping))
        {
            ProcessStartInfo rawUnloadInfo = new()
            {
                FileName = VeraCryptPath,
                Arguments = $"/quit /dismount {_rawLowerLetter}"
            };
            Process rawUnloadProcess = Process.Start(rawUnloadInfo) ?? throw new InvalidOperationException();
            rawUnloadProcess.WaitForExit();
        }

        if (DisconnectButton.IsEnabled)
        {
            _connection.Dispose();
        }
    }

    // Custom logging function
    public void LoggingEvent(object log, string login, string result = "")
    {
        // Configuration du FileSystemWatcher
        FileSystemWatcher fileSystemWatcher = new(Loggingfolderpath)
        {
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName,
            Filter = "logging.log"
        };

        fileSystemWatcher.Created += (sender, e) =>
        {
            if (e.Name == "logging.log")
            {
                fileSystemWatcher.EnableRaisingEvents = false;
            }
        };

        string logFilePath = Path.Combine(Loggingfolderpath, "logging.log");

        // Creating the necessary file
        if (!File.Exists(logFilePath))
        {
            using (FileStream fs = new(logFilePath, FileMode.Create, FileAccess.Write,
                       FileShare.ReadWrite)) ;
        }

        using (FileStream fs = new(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        using (StreamWriter sw = new(fs))
        {
            if (log is string logString && logString == "Connexion")
            {
                sw.WriteLine(
                    $"{DateTime.Now} - CONNEXION OCCURRED - HOST: {_computerName} - USER: {login} - LOG: {result}");
            }
            else
            {
                sw.WriteLine(
                    $"{DateTime.Now} - ERROR OCCURRED - HOST: {_computerName} - USER: {login} - LOG: {log} - RESULT: {result}");
            }
        }
    }

    // Password initialization
    private Object InitPassword(string projectName)
    {
        try
        {
            // Create the folder path
            string projectFolderPath = Path.Combine(_localProjectFolderPath, projectName);
            string fullPath = Path.Combine(projectFolderPath, string.Concat(projectName, "_PASSWORD.txt"));

            if (File.Exists(fullPath))
            {
                string password = File.ReadAllText(fullPath);
                return password;
            }
            else
            {
                string passwordNum = "0123456789";
                string passwordUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                string passwordSpecial = "!@#$%^&*()-_=+<,>.";
                string passwordLower = "abcdefghijklmnopqrstuvwxyz";

                if (Path.Exists(projectFolderPath) == false)
                {
                    // Create the project folder
                    Directory.CreateDirectory(projectFolderPath);
                }

                Random random = new();
                const int intNumber = 6;
                const int specialNumber = 11;
                const int upperNumber = 4;
                const int lowerNumber = 4;

                StringBuilder passwordBuilder = new(PasswordLength);

                for (int i = 0; i < intNumber; i++)
                {
                    passwordBuilder.Append(passwordNum[random.Next(passwordNum.Length)]);
                }

                for (int i = 0; i < specialNumber; i++)
                {
                    passwordBuilder.Append(passwordUpper[random.Next(passwordUpper.Length)]);
                }

                for (int i = 0; i < upperNumber; i++)
                {
                    passwordBuilder.Append(passwordLower[random.Next(passwordLower.Length)]);
                }

                for (int i = 0; i < lowerNumber; i++)
                {
                    passwordBuilder.Append(passwordSpecial[random.Next(passwordSpecial.Length)]);
                }

                for (int i = passwordBuilder.Length; i < PasswordLength; i++)
                {
                    string allCharacters = passwordNum + passwordUpper + passwordLower + passwordSpecial;
                    passwordBuilder.Append(allCharacters[random.Next(allCharacters.Length)]);
                }

                for (int i = passwordBuilder.Length - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (passwordBuilder[i], passwordBuilder[j]) = (passwordBuilder[j], passwordBuilder[i]);
                }

                string password = passwordBuilder.ToString();

                File.WriteAllText(fullPath, password);
                return password;
            }
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    // Find all the children of a specific type
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
            if (child != null && child is T)
            {
                yield return (T)child;
            }

            foreach (T childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    // Calculate the size of a directory
    private static async Task<long> _CalcDirSizeAsync(DirectoryInfo di, bool recurse = true)
    {
        long size = 0;
        FileInfo[] fiEntries = di.GetFiles();
        foreach (var fiEntry in fiEntries)
        {
            Interlocked.Add(ref size, fiEntry.Length);
        }

        if (recurse)
        {
            DirectoryInfo[] diEntries = di.GetDirectories("*.*", SearchOption.TopDirectoryOnly);
            await Task.WhenAll(diEntries.Select(async diEntry =>
            {
                if ((diEntry.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) return;
                long subdirSize = await _CalcDirSizeAsync(diEntry, true);
                Interlocked.Add(ref size, subdirSize);
            }));
        }

        return size;
    }

    // Update the progress bar
    private void ProgressUpdate(ProgressBar progressBar, Label progressStatus, string status, bool show)
    {
        progressStatus.Content = status;
        if (show)
        {
            progressBar.Visibility = Visibility.Visible;
            progressBar.IsIndeterminate = true;
        }
        else
        {
            progressBar.Visibility = Visibility.Hidden;
            progressBar.IsIndeterminate = false;
        }
    }

    // Adjust the size of the containers
    private long AjustedSize(long totalSize, double upperfactor, double lowerfactor)
    {
        if (totalSize < 10000000)
        {
            return 10000000;
        }

        if (totalSize < 50000000)
        {
            return (long)Math.Ceiling(totalSize * upperfactor);
        }

        return (long)Math.Ceiling(totalSize * lowerfactor);
    }

    // Lock the UI during a process
    public void LockUi(bool state)
    {
        var connectButtonState = ConnectButton.IsEnabled;
        var disconnectButtonState = DisconnectButton.IsEnabled;
        if (state)
        {
            foreach (TextBox textBox in FindVisualChildren<TextBox>(this))
            {
                textBox.IsEnabled = false;
            }

            foreach (Button button in FindVisualChildren<Button>(this))
            {
                button.IsEnabled = false;
            }
            PasswordBox.IsEnabled = false;
            FileListContent.IsEnabled = false;
            DriveList.IsEnabled = false;
            WorkSlider.IsEnabled = false;
            RawSlider.IsEnabled = false;

        }
        else
        {
            foreach (TextBox textBox in FindVisualChildren<TextBox>(this))
            {
                textBox.IsEnabled = true;
            }

            foreach (Button button in FindVisualChildren<Button>(this))
            {
                button.IsEnabled = true;
            }
            ConnectButton.IsEnabled =  connectButtonState;
            DisconnectButton.IsEnabled = disconnectButtonState;

            PasswordBox.IsEnabled = true;
            FileListContent.IsEnabled = true;
            DriveList.IsEnabled = true;
            WorkSlider.IsEnabled = true;
            RawSlider.IsEnabled = true;
        }
        PauseButton.IsEnabled = false;
    }

    #region Onclick and Onvalue changed methods

    // Used when the user wants to connect to a remote computer
    private void ConnectButton_OnClick(object sender, RoutedEventArgs e)
    {
        DriveList.Items.Clear();
        _computerName = ComputerName.Text.ToUpper();
        Login = LoginBox.Text.ToUpper();
        _password = PasswordBox.Password;
        try
        {
            if (_computerName.Contains(';') || _computerName.Contains('&') || Login.Contains(';') ||
                Login.Contains('&') || _password.Contains(';') || _password.Contains('&'))
            {
                MessageBox.Show("Invalid characters in the fields", "ERROR", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (_computerName != "" && Login != "" && _password != "")
            {
                var ping = new Ping();
                PingReply reply = ping.Send(_computerName);
                if (reply.Status != IPStatus.Success)
                {
                    MessageBox.Show("Cannot join " + _computerName, "ERROR", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    throw new($"Cannot join {_computerName} - {reply.Status}");
                }

                try
                {
                    _connection = NetworkShareAccesser.Access(remoteComputerName: _computerName, userName: Login,
                        password: _password);
                }

                catch (Win32Exception ex)
                {
                    string exMsg = GetSystemMessage((uint)ex.NativeErrorCode);
                    LoggingEvent("Connexion", Login, $"CAN BE IGNORED: {exMsg}");
                }

                ProcessStartInfo processInfo = new()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c net view {_computerName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = Process.Start(processInfo)!;
                // Lecture de la sortie
                string output = process!.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    throw new(error);
                }

                // Traitement des noms de partage
                foreach (string line in output.Split('\n'))
                {
                    // Suppression des espaces inutiles
                    string trimmedLine = line.Trim();

                    // Vérification que la ligne contient un partage valide
                    if (!string.IsNullOrEmpty(trimmedLine) &&
                        !trimmedLine.StartsWith("Ressources partagées") &&
                        !trimmedLine.StartsWith("Nom du partage") &&
                        !trimmedLine.StartsWith("La commande") &&
                        !trimmedLine.StartsWith("-"))
                    {
                        // Séparation des mots dans la ligne
                        string[] parts = trimmedLine.Split([' '], StringSplitOptions.RemoveEmptyEntries);

                        // Vérification si le dernier mot est "Disque"
                        if (parts.Length > 1 && (parts[^1].Equals("Disque", StringComparison.OrdinalIgnoreCase) ||
                                                 parts[^1].Equals("(UNC)", StringComparison.OrdinalIgnoreCase)))
                        {
                            // Afficher le premier mot comme nom du partage
                            DriveList.Items.Add(parts[0]);
                        }
                    }
                }

                ComputerName.IsEnabled = false;
                LoginBox.Text = "";
                PasswordBox.Password = "";
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                MessageBox.Show($"Connexion to {_computerName} successful", "SUCCESS", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                LoggingEvent("Connexion", Login, "Success");
            }
            else
            {
                MessageBox.Show("Please fill all the fields", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            LoggingEvent("Connexion", Login, ex.Message + "\n" + ex.StackTrace);
        }
    }

    // Used when the user wants to disconnect from the remote computer
    private void DisconnectButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _connection.Dispose();
        }
        catch (Win32Exception ex)
        {
            string exMsg = GetSystemMessage((uint)ex.NativeErrorCode);
            MessageBox.Show(exMsg, "ERROR", MessageBoxButton.OK,
                MessageBoxImage.Error);
            LoggingEvent(exMsg, Login!, ex.StackTrace!);
        }

        MessageBox.Show("Disconnected", "SUCCESS", MessageBoxButton.OK, MessageBoxImage.Information);
        DisconnectButton.IsEnabled = false;
        ConnectButton.IsEnabled = true;
        DriveList.Items.Clear();
        FileListContent.Items.Clear();
        ProjectBox.Text = "";
        ProjectDisplayed.Text = "";
        ProjectDisplayed.ToolTip = "";
        PasswordBox.Password = "";
        ComputerName.Text = "";
        RawSlider.Value = 1.09;
        WorkSlider.Value = 1.09;
        ComputerName.IsEnabled = true;
    }

    // Used when the user wants to search for a project
    private void SearchButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _totalSize = 0;
            string computer = _computerName!;
            var selectedDrive = DriveList.SelectedItem.ToString();

            if (string.IsNullOrEmpty(selectedDrive))
            {
                MessageBox.Show("No Drive selected", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProjectDisplayed.Text = "";
            FileListContent.Items.Clear();

            var dialog = new FolderBrowserDialog();
            dialog.InitialDirectory = @$"\\{computer}\{selectedDrive}\";
            dialog.ShowDialog();
            try
            {
                RemoteProjectPath = dialog.SelectedPath;
                ProjectBox.Text = Path.GetFileName(RemoteProjectPath!);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ProjectDisplayed.ToolTip = $"The full path is:\n{RemoteProjectPath}";

            string projectName = Path.GetFileName(RemoteProjectPath!);
            ProjectDisplayed.Text = projectName.Replace(" ", "_");

            DirectoryInfo directoryInfo = new(RemoteProjectPath);
            directoryInfo.GetDirectories().ToList()
                .ForEach(d => FileListContent.Items.Add(new Dir { Name = d.Name, Path = d.FullName }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            LoggingEvent(ex.Message, Login!, ex.StackTrace!);
        }
    }

    // Used when the user wants to change the default save path
    private void BrowseButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new FolderBrowserDialog();
        dialog.RootFolder = Environment.SpecialFolder.MyComputer;
        dialog.SelectedPath = _localProjectFolderPath;
        dialog.ShowNewFolderButton = true;
        dialog.ShowDialog();
        try
        {
            // Check if the path contains spaces
            if (dialog.SelectedPath.Contains(' '))
            {
                MessageBox.Show($"The path contains spaces, please choose another one:\n{dialog.SelectedPath}", "WARNING", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SavingField.Text = dialog.SelectedPath;
            _localProjectFolderPath = dialog.SelectedPath;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            _localProjectFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            SavingField.Text = _localProjectFolderPath;
        }
    }

    // Used when the user wants to encrypt ad load the data container
    private async void EncryptLoadButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string projectName = ProjectDisplayed.Text;
            if (string.IsNullOrEmpty(projectName))
            {
                MessageBox.Show("No project selected", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var password = InitPassword(projectName);
            // Stop the process if the password is an exception because of errors during the container creation with
            // VeraCrypt are not specified
            if (password.GetType() == typeof(Exception))
            {
                throw (Exception)password;
            }

            List<Dir> workFolderItems = new();
            List<Dir> rawFolderItems = new();
            foreach (Dir selected in FileListContent.SelectedItems)
            {
                DirectoryInfo directoryInfo = new(selected.Path);
                // Get the size of the directory
                long size = await _CalcDirSizeAsync(directoryInfo, true).ConfigureAwait(false);
                if (selected.Name.StartsWith("0"))
                {
                    rawFolderItems.Add(new() { Name = selected.Name, Path = selected.Path, Size = size });
                }
                else
                {
                    workFolderItems.Add(new() { Name = selected.Name, Path = selected.Path, Size = size });
                }
            }

            long workSize = 0;
            long rawSize = 0;
            if (workFolderItems.Count == 0 && rawFolderItems.Count == 0)
            {
                MessageBox.Show("No folder selected", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Display the progress bar
            LockUi(true);
            ProgressUpdate(ProgressBar, ProgressStatus, "Encrypting...", true);
            if (workFolderItems.Count > 0)
            {
                foreach (Dir dir in workFolderItems)
                {
                    Interlocked.Add(ref _totalSize, dir.Size);
                    Interlocked.Add(ref workSize, dir.Size);
                }

                workSize = AjustedSize(workSize, WorkUpperSizeFactor, _workLowerSizeFactor);
                string workDataName = $"{projectName}_WORK-DATA.hc";
                string workDataPath = Path.Combine(_localProjectFolderPath, projectName, workDataName);
                if (!Path.Exists(workDataPath))
                {
                    try
                    {
                        string[] workDataArgs =
                        [
                            $"/create {workDataPath}",
                            $"/password {password}",
                            $"/size {workSize}",
                            "/encryption AES",
                            "/hash sha512",
                            "/filesystem exFAT",
                            "/force",
                            "/silent"
                        ];
                        // Careful with string manipulation because VeraCrypt Format is a hell to debug
                        string workDataArgsString = string.Join(" ", workDataArgs);
                        ProcessStartInfo workDataProcessInfo = new()
                        {
                            FileName = VeraCryptFormatPath,
                            Arguments = workDataArgsString,
                        };
                        Process workDataProcess = Process.Start(workDataProcessInfo)!;
                        await workDataProcess.WaitForExitAsync();
                        if (workDataProcess.ExitCode != 0)
                        {
                            throw new("VERACRYPT ERROR");
                        }
                    }
                    catch (Exception ex)
                    {
                        ProgressUpdate(ProgressBar, ProgressStatus, "Error while encrypting the WORK DATA", false);
                        MessageBox.Show($"Error while encrypting the WORK DATA: {ex.Message}", "ERROR",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        LoggingEvent(ex.Message, Login!, ex.StackTrace!);
                        LockUi(false);
                        return;
                    }
                }

            }

            if (rawFolderItems.Count > 0)
            {
                foreach (Dir dir in rawFolderItems)
                {
                    Interlocked.Add(ref _totalSize, dir.Size);
                    Interlocked.Add(ref rawSize, dir.Size);
                }

                rawSize = AjustedSize(rawSize, RawUpperSizeFactor, _rawLowerSizeFactor);
                string rawDataName = $"{projectName}_RAW-DATA.hc";
                string rawDataPath = Path.Combine(_localProjectFolderPath, projectName, rawDataName);
                if (!Path.Exists(rawDataPath))
                {
                    try
                    {
                        string[] rawDataArgs =
                        [
                            $"/create {rawDataPath}",
                            $"/password {password}",
                            $"/size {rawSize}",
                            "/encryption AES",
                            "/hash sha512",
                            "/filesystem exFAT",
                            "/force",
                            "/silent"
                        ];
                        // Careful with string manipulation because VeraCrypt Format is a hell to debug
                        string rawDataArgsString = string.Join(" ", rawDataArgs);
                        ProcessStartInfo rawDataProcessInfo = new()
                        {
                            FileName = VeraCryptFormatPath,
                            Arguments = rawDataArgsString
                        };
                        Process rawDataProcess = Process.Start(rawDataProcessInfo)!;
                        await rawDataProcess.WaitForExitAsync();

                        if (rawDataProcess.ExitCode != 0)
                        {
                            throw new("VERACRYPT ERROR");
                        }
                    }
                    catch (Exception ex)
                    {
                        ProgressUpdate(ProgressBar, ProgressStatus, "Error while encrypting the RAW DATA", false);
                        MessageBox.Show($"Error while encrypting the RAW DATA: {ex.Message}", "ERROR",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        LoggingEvent(ex.Message, Login!, ex.StackTrace!);
                        LockUi(false);
                        return;
                    }
                }
            }

            // Hides it when the encryption is done
            ProgressUpdate(ProgressBar, ProgressStatus, "Encryption completed", false);

            //
            // LOADING THE CONTAINERS
            //

            DriveInfo[] allDrives = DriveInfo.GetDrives();
            // Removing A and B because they are reserved for floppy disks
            // Removing C because it is the system drive
            string letters = "DEFGHIJKLMNOPQRSTUVWXYZ";

            foreach (DriveInfo d in allDrives)
            {
                if (d.IsReady)
                {
                    // Removing the used drives letters
                    letters = letters.Replace(d.Name[0].ToString(), "");
                }
            }
            if (letters.Length < 2)
            {
                MessageBox.Show("No free drive letter available", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                LoggingEvent("Error", Login!, "No free drive letter available");
                LockUi(false);
                return;
            }

            ProgressUpdate(ProgressBar, ProgressStatus, "Loading...", true);
            var work = $"{projectName}_WORK-DATA.hc";
            var raw = $"{projectName}_RAW-DATA.hc";
            string workVolumePath = Path.Combine(_localProjectFolderPath, projectName, work);
            string rawVolumePath = Path.Combine(_localProjectFolderPath, projectName, raw);

            if (workFolderItems.Count > 0)
            {
                // Get the first letter of the free drive letters
                _workLowerLetter = letters.First().ToString().ToLower();
                _workMapping = $"{letters.First().ToString()}:\\";
                // Remove the first letter of the free drive letters
                letters = letters[1..];
                ProcessStartInfo workLoadInfo = new()
                {
                    FileName = VeraCryptPath,
                    Arguments = $"/q /s /v {workVolumePath} /l {_workLowerLetter} /p {password} /m rm"
                };
                Process workLoadProcess = Process.Start(workLoadInfo) ?? throw new InvalidOperationException();
                await workLoadProcess.WaitForExitAsync();
                if (Path.Exists(_workMapping) == false)
                {
                    ProgressUpdate(ProgressBar, ProgressStatus, "Error while loading the WORK DATA container", false);
                    MessageBox.Show("Error while loading the WORK DATA container", "ERROR", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    LoggingEvent("Error", Login!, "Error while loading the WORK DATA container");
                    LockUi(false);
                    return;
                }
            }

            if (rawFolderItems.Count > 0)
            {
                // Get the second letter of the free drive letters
                _rawLowerLetter = letters.First().ToString().ToLower();
                _rawMapping = $"{letters.First().ToString()}:\\";
                ProcessStartInfo rawLoadInfo = new()
                {

                    FileName = VeraCryptPath,
                    Arguments = $"/q /s /v {rawVolumePath} /l {_rawLowerLetter} /p {password} /m rm"
                };
                Process rawLoadProcess = Process.Start(rawLoadInfo) ?? throw new InvalidOperationException();
                await rawLoadProcess.WaitForExitAsync();
                if (Path.Exists(_rawMapping) == false)
                {
                    ProgressUpdate(ProgressBar, ProgressStatus, "Error while loading the RAW DATA container", false);
                    MessageBox.Show("Error while loading the RAW DATA container", "ERROR", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    LoggingEvent("Error", Login!, "Error while loading the RAW DATA container");
                    LockUi(false);
                    return;
                }
            }

            ProgressUpdate(ProgressBar, ProgressStatus, "Loading complete", false);
            MessageBox.Show("The VeraCrypt containers have been Loaded successfully.", "SUCCESS", MessageBoxButton.OK,
                MessageBoxImage.Information);
            LockUi(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error while encrypting and loading {ex.Message}", "ERROR", MessageBoxButton.OK,
                MessageBoxImage.Error);
            LoggingEvent(ex.Message, Login!, ex.StackTrace!);
            LockUi(false);
        }
    }


    // Used when the user wants to copy the data
    private async void CopyUnloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Path.Exists(_workMapping) == false && Path.Exists(_rawMapping) == false)
            {
                MessageBox.Show("Theres no container loaded, copy aborted", "WARNING", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            LockUi(true);
            PauseButton.IsEnabled = true;
            ProgressStatus.Content = "Copying...";
            ProgressBar.Value = 0;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = false;

            List<Dir> selectedItems = FileListContent.SelectedItems.Cast<Dir>().ToList();
            _fileCopyHandler = new(RemoteProjectPath!, _workMapping, _rawMapping, ProgressBar, Login!,
                _totalSize, this, maxConcurrency: 4, customBufferSize: Buffersize);
            await _fileCopyHandler.CopyFilesMultithreaded(selectedItems);

            DriveInfo[] allDrives = DriveInfo.GetDrives();
            if (allDrives.Any(drive => drive.Name == _workMapping))
            {
                string[] argsList =
                [
                    "/quit",
                    "/dismount",
                    $"{_workLowerLetter}"
                ];
                string argsString = string.Join(" ", argsList);
                ProcessStartInfo workUnloadInfo = new()
                {
                    FileName = VeraCryptPath,
                    Arguments = argsString
                };
                Process workUnloadProcess = Process.Start(workUnloadInfo) ?? throw new InvalidOperationException();
                await workUnloadProcess.WaitForExitAsync();
            }

            if (allDrives.Any(drive => drive.Name == _rawMapping))
            {
                string[] argsList =
                [
                    "/quit",
                    "/dismount",
                    $"{_rawLowerLetter}"
                ];
                string argsString = string.Join(" ", argsList);
                ProcessStartInfo rawUnloadInfo = new()
                {
                    FileName = VeraCryptPath,
                    Arguments = argsString
                };
                Process workUnloadProcess = Process.Start(rawUnloadInfo) ?? throw new InvalidOperationException();
                await workUnloadProcess.WaitForExitAsync();
            }


            MessageBox.Show("The copy is done and the VeraCrypt containers have been unloaded successfully.", "SUCCESS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            ProgressBar.Visibility = Visibility.Hidden;
            MessageBox.Show(@" /!\ PLEASE SAVE THE PASSWORD IN THE SHARED LASTPASS /!\", "WARNING", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            ProgressStatus.Content = @" /!\ PLEASE SAVE THE PASSWORD IN THE SHARED LASTPASS /!\";
            LockUi(false);

        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error while copying {ex.Message}", "FATAL", MessageBoxButton.OK, MessageBoxImage.Error);
            LoggingEvent(ex.Message, Login!, ex.StackTrace!);
            LockUi(false);
            _fileCopyHandler.Pause();
        }
    }

    // Used when the user wants to pause the copy
    private void PauseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (PauseButton.Content.ToString() == "Resume")
        {
            _fileCopyHandler.Resume();
            PauseButton.Content = "Pause";
            ProgressStatus.Content = "Copying...";
            ProgressBar.Foreground = _windowsGreen;
            return;
        }
        _fileCopyHandler.Pause();
        PauseButton.Content = "Resume";
        ProgressStatus.Content = "Copy paused";
        ProgressBar.Foreground = _windowsOrange;
    }

    // Used when the user wants to change the size of the work containers
    private void WorkSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = Math.Round(e.NewValue, 2, MidpointRounding.AwayFromZero);
        WorkFactor.Text = $"WORK DATA SIZE: {value.ToString(CultureInfo.InvariantCulture)}";
        _workLowerSizeFactor = e.NewValue;
    }

    // Used when the user wants to change the size of the raw containers
    private void RawSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = Math.Round(e.NewValue, 2, MidpointRounding.AwayFromZero);
        RawFactor.Text = $"RAW DATA SIZE: {value.ToString(CultureInfo.InvariantCulture)}";
        _rawLowerSizeFactor = e.NewValue;
    }

    #endregion
}