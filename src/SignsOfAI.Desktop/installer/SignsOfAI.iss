; The Windows installer for Signs of AI Writing.
;
; It exists because the audience this app is for — a teacher who wants to open a folder of PDFs —
; was being asked to right-click a zip, tick Unblock, run Get-FileHash in PowerShell, extract it,
; and then keep the folder together, because the app's web assets must sit next to the executable.
; An installer does the last three for them and cannot get the folder wrong.
;
; It does NOT make the app trusted. It is unsigned, exactly like the executable inside it, so
; SmartScreen still warns on first run. Only code signing removes that, and the download page says so.
;
; Built by .github/workflows/desktop-release.yml, which passes the two values that change:
;
;     ISCC /DAppVersion=0.8.1 /DStaging=staging\SignsOfAI-Desktop-0.8.1-win-x64 SignsOfAI.iss
;
; No version is written in this file. It is already typed in seven places that ReleaseVersionTests
; holds together, and an eighth copy here is how one of them would end up disagreeing.

#ifndef AppVersion
  #error Pass the version on the command line: /DAppVersion=0.8.1
#endif
#ifndef Staging
  #error Pass the publish folder on the command line: /DStaging=path\to\publish
#endif

[Setup]
; Never change this. It is how Windows recognises a newer installer as the same app to upgrade,
; rather than installing a second copy beside the first.
AppId={{FDD6B2FD-9E64-4C17-BA52-C0BFB387BA85}
AppName=Signs of AI Writing
AppVersion={#AppVersion}
AppVerName=Signs of AI Writing {#AppVersion}
AppPublisher=PeopleWorks
AppPublisherURL=https://github.com/peopleworks/SignsofAI
AppSupportURL=https://github.com/peopleworks/SignsofAI/issues
AppUpdatesURL=https://peopleworks.github.io/SignsofAI/download

; For the current user only, into their own profile. School laptops are often managed, and a teacher
; who is not an administrator must still be able to install it without asking IT.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\SignsOfAI
DisableProgramGroupPage=yes
DisableDirPage=auto

; Windows 10 1809, the oldest the download page promises; 64-bit only, like the build.
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputBaseFilename=SignsOfAI-Desktop-{#AppVersion}-win-x64-Setup
SetupIconFile=..\appicon.ico
UninstallDisplayIcon={app}\SignsOfAI.Desktop.exe
UninstallDisplayName=Signs of AI Writing
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The whole publish tree, subfolders included — wwwroot above all. That is the folder the zip told
; people to keep together, and here it cannot be split.
Source: "{#Staging}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Signs of AI Writing"; Filename: "{app}\SignsOfAI.Desktop.exe"
Name: "{autodesktop}\Signs of AI Writing"; Filename: "{app}\SignsOfAI.Desktop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SignsOfAI.Desktop.exe"; Description: "{cm:LaunchProgram,Signs of AI Writing}"; Flags: nowait postinstall skipifsilent
