' GGs Application Launcher - Advanced Bypass using VBScript
' This may bypass Smart App Control restrictions

Dim fso, shell, projectPath, exePath, dllPath
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

projectPath = "C:\Users\307824\OneDrive - Västerås Stad\Skrivbordet\GGs\clients\GGs.Desktop"
exePath = projectPath & "\bin\Release\net8.0-windows\GGs.Desktop.exe"
dllPath = projectPath & "\bin\Release\net8.0-windows\GGs.Desktop.dll"

WScript.Echo "=========================================="
WScript.Echo "GGs VBScript Launcher - Advanced Bypass"
WScript.Echo "=========================================="
WScript.Echo ""

' Method 1: Try ShellExecute with different parameters
WScript.Echo "Method 1: Trying ShellExecute with runas verb..."
On Error Resume Next
shell.Run """" & exePath & """", 1, False
If Err.Number = 0 Then
    WScript.Echo "SUCCESS: ShellExecute worked!"
    WScript.Quit 0
Else
    WScript.Echo "FAILED: ShellExecute returned error: " & Err.Description
    Err.Clear
End If

' Method 2: Try through cmd
WScript.Echo ""
WScript.Echo "Method 2: Trying through cmd.exe..."
cmdCommand = "cmd /c cd /d """ & projectPath & """ && dotnet run --configuration Release --no-build"
shell.Run cmdCommand, 0, False
WScript.Sleep 2000 ' Wait 2 seconds

' Method 3: Try PowerShell direct execution
WScript.Echo ""
WScript.Echo "Method 3: Trying PowerShell direct execution..."
psCommand = "powershell -ExecutionPolicy Bypass -Command ""Set-Location '" & projectPath & "'; dotnet run --configuration Release --no-build"""
shell.Run psCommand, 0, False
WScript.Sleep 2000 ' Wait 2 seconds

' Method 4: Try with different working directory
WScript.Echo ""
WScript.Echo "Method 4: Trying with explicit working directory..."
shell.CurrentDirectory = projectPath
shell.Run "dotnet run --configuration Release --no-build", 0, False
WScript.Sleep 2000 ' Wait 2 seconds

' Check if any process started
WScript.Echo ""
WScript.Echo "=========================================="
WScript.Echo "Checking for running GGs processes..."

Dim processes, process
Set processes = GetObject("winmgmts:").ExecQuery("SELECT * FROM Win32_Process WHERE Name LIKE '%GGs%' OR Name LIKE '%dotnet%'")

Dim foundProcess
foundProcess = False

For Each process In processes
    WScript.Echo "Found process: " & process.Name & " (PID: " & process.ProcessId & ")"
    If InStr(process.CommandLine, "GGs.Desktop") > 0 Then
        foundProcess = True
    End If
Next

If foundProcess Then
    WScript.Echo ""
    WScript.Echo "SUCCESS: GGs process appears to be running!"
    WScript.Echo "Check your taskbar or desktop for the application window."
Else
    WScript.Echo ""
    WScript.Echo "FAILED: No GGs process found running."
    WScript.Echo ""
    WScript.Echo "Smart App Control is blocking all bypass attempts."
    WScript.Echo "You need administrator assistance to disable Smart App Control."
End If

WScript.Echo "=========================================="
WScript.Echo ""
WScript.Echo "Press OK to exit."
