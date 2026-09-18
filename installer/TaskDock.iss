#define MyAppName "TaskDock"
#define MyAppVersion "0.1.3"
#define MyAppPublisher "TaskDock"
#define MyAppExeName "TaskDock.exe"

[Setup]
AppId={{D8151432-BA6C-4DBF-B147-1647329D8947}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\TaskDock
DefaultGroupName=TaskDock
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=TaskDock-Setup-{#MyAppVersion}-win-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\TaskDock\Assets\TaskDock.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: unchecked

[Files]
Source: "..\artifacts\portable\TaskDock-0.1.3\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "portable.flag"

[Icons]
Name: "{group}\TaskDock"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\TaskDock"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 TaskDock"; Flags: nowait postinstall skipifsilent
