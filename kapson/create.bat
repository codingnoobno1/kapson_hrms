@echo off
setlocal EnableExtensions

rem ==================================================================
rem  Vispl HR Management solution scaffolder
rem
rem  Initial runtime model:
rem      one executable : Vispl.HRManagement.Host
rem      one process    : Vispl.HRManagement.Host.exe
rem      one port       : http://localhost:7000
rem
rem  Usage: create.bat [--no-build]
rem ==================================================================

pushd "%~dp0"
set "SOLUTION=Kapson.HRManagement.sln"
set "FRAMEWORK=net9.0"
set "SKIP_BUILD=0"

if /I "%~1"=="--no-build" set "SKIP_BUILD=1"
if not "%~1"=="" if /I not "%~1"=="--no-build" (
    echo ERROR: Unknown option "%~1".
    echo Usage: create.bat [--no-build]
    goto :fail
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: The .NET SDK was not found in PATH.
    goto :fail
)

dotnet --list-sdks | findstr /R /B "9\." >nul
if errorlevel 1 (
    echo ERROR: A .NET 9 SDK is required.
    dotnet --list-sdks
    goto :fail
)

if not exist ".scaffold\Directory.Build.props" (
    echo ERROR: The .scaffold template directory is missing.
    goto :fail
)

echo.
echo [1/8] Creating solution and shared build configuration...
if not exist "%SOLUTION%" (
    dotnet new sln -n Kapson.HRManagement
    if errorlevel 1 goto :fail
) else (
    echo       %SOLUTION% already exists.
)

if not exist ".gitignore" (
    dotnet new gitignore
    if errorlevel 1 goto :fail
)
call :CopyIfMissing ".scaffold\Directory.Build.props" "Directory.Build.props" || goto :fail

echo.
echo [2/8] Creating the single server executable...
call :CreateProject web Vispl.HRManagement.Host || goto :fail

echo.
echo [3/8] Creating server libraries...
call :CreateProject razorclasslib Vispl.HRManagement.UI          || goto :fail
call :CreateProject classlib      Vispl.HRManagement.Identity    || goto :fail
call :CreateProject classlib      Vispl.HRManagement.Composition || goto :fail
call :CreateProject classlib      Vispl.HRManagement.Worker      || goto :fail
call :CreateProject classlib      Vispl.HRManagement.Contracts   || goto :fail
call :CreateProject classlib      Vispl.HRManagement.UIVM        || goto :fail
call :CreateProject classlib      Vispl.HRManagement.BM.ITF      || goto :fail
call :CreateProject classlib      Vispl.HRManagement.BM          || goto :fail
call :CreateProject classlib      Vispl.HRManagement.DL.ITF      || goto :fail
call :CreateProject classlib      Vispl.HRManagement.DL          || goto :fail
call :CreateProject classlib      Vispl.HRManagement.VO          || goto :fail
call :CreateProject classlib      Vispl.HRManagement.Resx        || goto :fail

echo.
echo [4/8] Creating client and tool projects...
rem Mobile is an API-client shell until MAUI or another client is selected.
call :CreateProject classlib Vispl.HRManagement.Mobile     || goto :fail
call :CreateProject console  Vispl.HRManagement.SeedExport || goto :fail

echo.
echo [5/8] Creating aligned module folders...
call :MakeDir "Vispl.HRManagement.Host\Routing"
call :MakeDir "Vispl.HRManagement.Host\HealthChecks"
call :MakeDir "Vispl.HRManagement.Host\Configuration"
call :MakeDir "Vispl.HRManagement.Identity\Shared\Abstractions"
call :MakeDir "Vispl.HRManagement.Identity\Shared\DataProtection"
call :MakeDir "Vispl.HRManagement.Identity\Shared\Common"

for %%M in (Admin Recruitment Candidate Employee) do (
    call :MakeDir "Vispl.HRManagement.Identity\%%M\Authentication"
    call :MakeDir "Vispl.HRManagement.Identity\%%M\Authorization"
    call :MakeDir "Vispl.HRManagement.Identity\%%M\Tokens"
    call :MakeDir "Vispl.HRManagement.Identity\%%M\Cookies"
)

for %%M in (Shared Admin Recruitment Candidate Employee) do (
    call :MakeDir "Vispl.HRManagement.Composition\%%M"
    call :MakeDir "Vispl.HRManagement.Contracts\%%M"
    call :MakeDir "Vispl.HRManagement.Mobile\%%M"
    call :MakeDir "Vispl.HRManagement.UIVM\%%M"
    call :MakeDir "Vispl.HRManagement.BM.ITF\%%M"
    call :MakeDir "Vispl.HRManagement.BM\%%M"
    call :MakeDir "Vispl.HRManagement.DL.ITF\%%M"
    call :MakeDir "Vispl.HRManagement.DL\%%M"
    call :MakeDir "Vispl.HRManagement.VO\%%M"
    call :MakeDir "Vispl.HRManagement.Resx\%%M"
    call :MakeDir "Vispl.HRManagement.Worker\%%M"
    call :MakeDir "Vispl.HRManagement.UI\Components\%%M"
)

call :MakeDir "Vispl.HRManagement.UI\Components\Shared\Pages"
call :MakeDir "Vispl.HRManagement.UI\Components\Candidate\Public\Pages"
call :MakeDir "Vispl.HRManagement.UI\Layout"
call :MakeDir "Vispl.HRManagement.SeedExport\Commands"
call :MakeDir "Vispl.HRManagement.SeedExport\Exporters"

rem Remove only disposable files created by the stock templates.
for %%P in (
    Vispl.HRManagement.Identity
    Vispl.HRManagement.Composition
    Vispl.HRManagement.Worker
    Vispl.HRManagement.Contracts
    Vispl.HRManagement.Mobile
    Vispl.HRManagement.UIVM
    Vispl.HRManagement.BM.ITF
    Vispl.HRManagement.BM
    Vispl.HRManagement.DL.ITF
    Vispl.HRManagement.DL
    Vispl.HRManagement.VO
    Vispl.HRManagement.Resx
) do (
    if exist "%%P\Class1.cs" del /q "%%P\Class1.cs"
)
if exist "Vispl.HRManagement.UI\Component1.razor" del /q "Vispl.HRManagement.UI\Component1.razor"
if exist "Vispl.HRManagement.UI\Component1.razor.css" del /q "Vispl.HRManagement.UI\Component1.razor.css"
if exist "Vispl.HRManagement.UI\ExampleJsInterop.cs" del /q "Vispl.HRManagement.UI\ExampleJsInterop.cs"
findstr /I /C:"Microsoft.AspNetCore.Components.Web" "Vispl.HRManagement.UI\Vispl.HRManagement.UI.csproj" >nul
if not errorlevel 1 (
    dotnet remove "Vispl.HRManagement.UI\Vispl.HRManagement.UI.csproj" package Microsoft.AspNetCore.Components.Web
    if errorlevel 1 goto :fail
)

echo.
echo [6/8] Installing composition and startup files...
call :InstallHostProgram || goto :fail
call :InstallLaunchSettings || goto :fail
call :CopyIfMissing ".scaffold\Identity\HrIdentityExtensions.cs" "Vispl.HRManagement.Identity\Shared\HrIdentityExtensions.cs" || goto :fail
call :CopyIfMissing ".scaffold\Identity\AdminIdentityModule.cs" "Vispl.HRManagement.Identity\Admin\AdminIdentityModule.cs" || goto :fail
call :CopyIfMissing ".scaffold\Identity\RecruitmentIdentityModule.cs" "Vispl.HRManagement.Identity\Recruitment\RecruitmentIdentityModule.cs" || goto :fail
call :CopyIfMissing ".scaffold\Identity\CandidateIdentityModule.cs" "Vispl.HRManagement.Identity\Candidate\CandidateIdentityModule.cs" || goto :fail
call :CopyIfMissing ".scaffold\Identity\EmployeeIdentityModule.cs" "Vispl.HRManagement.Identity\Employee\EmployeeIdentityModule.cs" || goto :fail
call :CopyIfMissing ".scaffold\Worker\HrWorkerExtensions.cs" "Vispl.HRManagement.Worker\Shared\HrWorkerExtensions.cs" || goto :fail
call :CopyIfMissing ".scaffold\Composition\HrManagementComposition.cs" "Vispl.HRManagement.Composition\Shared\HrManagementComposition.cs" || goto :fail
call :CopyIfMissing ".scaffold\Composition\AdminModule.cs" "Vispl.HRManagement.Composition\Admin\AdminModule.cs" || goto :fail
call :CopyIfMissing ".scaffold\Composition\RecruitmentModule.cs" "Vispl.HRManagement.Composition\Recruitment\RecruitmentModule.cs" || goto :fail
call :CopyIfMissing ".scaffold\Composition\CandidateModule.cs" "Vispl.HRManagement.Composition\Candidate\CandidateModule.cs" || goto :fail
call :CopyIfMissing ".scaffold\Composition\EmployeeModule.cs" "Vispl.HRManagement.Composition\Employee\EmployeeModule.cs" || goto :fail
call :CopyIfMissing ".scaffold\UI\App.razor" "Vispl.HRManagement.UI\Components\App.razor" || goto :fail
call :CopyIfMissing ".scaffold\UI\Routes.razor" "Vispl.HRManagement.UI\Components\Routes.razor" || goto :fail
call :CopyIfMissing ".scaffold\UI\Home.razor" "Vispl.HRManagement.UI\Components\Candidate\Public\Pages\KapsonHome.razor" || goto :fail
call :EnsureUiImports || goto :fail

echo.
echo [7/8] Applying project dependency rules...
call :AddReference Vispl.HRManagement.VO Vispl.HRManagement.Contracts || goto :fail
call :AddReference Vispl.HRManagement.BM.ITF Vispl.HRManagement.Contracts || goto :fail
call :AddReference Vispl.HRManagement.BM.ITF Vispl.HRManagement.VO        || goto :fail
call :AddReference Vispl.HRManagement.DL.ITF Vispl.HRManagement.Contracts || goto :fail
call :AddReference Vispl.HRManagement.DL.ITF Vispl.HRManagement.VO        || goto :fail
call :AddReference Vispl.HRManagement.BM Vispl.HRManagement.BM.ITF    || goto :fail
call :AddReference Vispl.HRManagement.BM Vispl.HRManagement.DL.ITF    || goto :fail
call :AddReference Vispl.HRManagement.BM Vispl.HRManagement.Contracts || goto :fail
call :AddReference Vispl.HRManagement.BM Vispl.HRManagement.VO        || goto :fail
call :AddReference Vispl.HRManagement.DL Vispl.HRManagement.DL.ITF    || goto :fail
call :AddReference Vispl.HRManagement.DL Vispl.HRManagement.Contracts || goto :fail
call :AddReference Vispl.HRManagement.DL Vispl.HRManagement.VO        || goto :fail
call :AddReference Vispl.HRManagement.UIVM Vispl.HRManagement.BM.ITF    || goto :fail
call :AddReference Vispl.HRManagement.UIVM Vispl.HRManagement.Contracts || goto :fail
call :AddReference Vispl.HRManagement.UIVM Vispl.HRManagement.VO        || goto :fail
call :AddReference Vispl.HRManagement.UI Vispl.HRManagement.UIVM        || goto :fail
call :AddReference Vispl.HRManagement.UI Vispl.HRManagement.BM.ITF      || goto :fail
call :AddReference Vispl.HRManagement.UI Vispl.HRManagement.Contracts   || goto :fail
call :AddReference Vispl.HRManagement.UI Vispl.HRManagement.VO          || goto :fail
call :AddReference Vispl.HRManagement.UI Vispl.HRManagement.Resx        || goto :fail
call :AddReference Vispl.HRManagement.Identity Vispl.HRManagement.Contracts || goto :fail
call :AddReference Vispl.HRManagement.Worker Vispl.HRManagement.BM.ITF    || goto :fail
call :AddReference Vispl.HRManagement.Worker Vispl.HRManagement.Contracts || goto :fail
call :AddReference Vispl.HRManagement.Worker Vispl.HRManagement.VO        || goto :fail

rem Composition is the only library that wires concrete implementations.
call :AddReference Vispl.HRManagement.Composition Vispl.HRManagement.UI       || goto :fail
call :AddReference Vispl.HRManagement.Composition Vispl.HRManagement.Identity || goto :fail
call :AddReference Vispl.HRManagement.Composition Vispl.HRManagement.Worker   || goto :fail
call :AddReference Vispl.HRManagement.Composition Vispl.HRManagement.BM       || goto :fail
call :AddReference Vispl.HRManagement.Composition Vispl.HRManagement.DL       || goto :fail
call :AddReference Vispl.HRManagement.Composition Vispl.HRManagement.Contracts || goto :fail

rem Host is the only server startup project today.
call :AddReference Vispl.HRManagement.Host Vispl.HRManagement.Composition || goto :fail
call :AddReference Vispl.HRManagement.Host Vispl.HRManagement.UI          || goto :fail

rem Mobile is an HTTP API client; SeedExport is an offline tool.
call :AddReference Vispl.HRManagement.Mobile Vispl.HRManagement.Contracts || goto :fail
call :AddReference Vispl.HRManagement.SeedExport Vispl.HRManagement.DL        || goto :fail
call :AddReference Vispl.HRManagement.SeedExport Vispl.HRManagement.Contracts || goto :fail

echo.
echo [8/8] Organizing, restoring and verifying the solution...
call :AddToSolution Vispl.HRManagement.Host        Hosts        || goto :fail
call :AddToSolution Vispl.HRManagement.UI          Clients      || goto :fail
call :AddToSolution Vispl.HRManagement.Mobile      Clients      || goto :fail
call :AddToSolution Vispl.HRManagement.Composition Composition  || goto :fail
call :AddToSolution Vispl.HRManagement.Identity    Core         || goto :fail
call :AddToSolution Vispl.HRManagement.Contracts   Core         || goto :fail
call :AddToSolution Vispl.HRManagement.VO          Core         || goto :fail
call :AddToSolution Vispl.HRManagement.UIVM        Presentation || goto :fail
call :AddToSolution Vispl.HRManagement.BM.ITF      Business     || goto :fail
call :AddToSolution Vispl.HRManagement.BM          Business     || goto :fail
call :AddToSolution Vispl.HRManagement.DL.ITF      Data         || goto :fail
call :AddToSolution Vispl.HRManagement.DL          Data         || goto :fail
call :AddToSolution Vispl.HRManagement.Resx        Shared       || goto :fail
call :AddToSolution Vispl.HRManagement.Worker      Shared       || goto :fail
call :AddToSolution Vispl.HRManagement.SeedExport  Tools        || goto :fail

if "%SKIP_BUILD%"=="1" (
    echo       Restore and build skipped by --no-build.
) else (
    dotnet restore "%SOLUTION%"
    if errorlevel 1 goto :fail
    dotnet build "%SOLUTION%" --no-restore
    if errorlevel 1 goto :fail
)

echo.
echo ==================================================================
echo  Scaffold completed successfully.
echo  Start the complete development server with one command:
echo.
echo    dotnet run --project Vispl.HRManagement.Host
echo.
echo  URL: http://localhost:7000
echo  Server executables started: 1
echo  YARP/Gateway dependency: none
echo ==================================================================
popd
exit /b 0

:CreateProject
set "TEMPLATE=%~1"
set "PROJECT=%~2"
if exist "%PROJECT%\%PROJECT%.csproj" (
    echo       %PROJECT% already exists.
    exit /b 0
)
dotnet new %TEMPLATE% -n "%PROJECT%" -o "%PROJECT%" -f "%FRAMEWORK%" --no-restore
exit /b %errorlevel%

:MakeDir
if not exist "%~1" mkdir "%~1" >nul
if not exist "%~1\.gitkeep" type nul > "%~1\.gitkeep"
exit /b 0

:CopyIfMissing
if exist "%~2" (
    echo       Keeping existing %~2
    exit /b 0
)
copy /y "%~1" "%~2" >nul
exit /b %errorlevel%

:InstallHostProgram
if not exist "Vispl.HRManagement.Host\Program.cs" (
    copy /y ".scaffold\Host\Program.cs" "Vispl.HRManagement.Host\Program.cs" >nul
    exit /b %errorlevel%
)
findstr /I /C:"Hello World" "Vispl.HRManagement.Host\Program.cs" >nul
if errorlevel 1 (
    echo       Keeping existing Vispl.HRManagement.Host\Program.cs
    exit /b 0
)
copy /y ".scaffold\Host\Program.cs" "Vispl.HRManagement.Host\Program.cs" >nul
exit /b %errorlevel%

:EnsureUiImports
findstr /I /C:"Microsoft.AspNetCore.Components.Routing" "Vispl.HRManagement.UI\_Imports.razor" >nul
if not errorlevel 1 exit /b 0
>>"Vispl.HRManagement.UI\_Imports.razor" echo @using Microsoft.AspNetCore.Components
>>"Vispl.HRManagement.UI\_Imports.razor" echo @using Microsoft.AspNetCore.Components.Routing
>>"Vispl.HRManagement.UI\_Imports.razor" echo @using Vispl.HRManagement.UI.Components
exit /b 0

:InstallLaunchSettings
findstr /I /C:"http://localhost:7000" "Vispl.HRManagement.Host\Properties\launchSettings.json" >nul
if not errorlevel 1 exit /b 0
copy /y ".scaffold\Host\launchSettings.json" "Vispl.HRManagement.Host\Properties\launchSettings.json" >nul
exit /b %errorlevel%

:AddReference
dotnet add "%~1\%~1.csproj" reference "%~2\%~2.csproj"
exit /b %errorlevel%

:AddToSolution
dotnet sln "%SOLUTION%" add "%~1\%~1.csproj" --solution-folder "%~2"
exit /b %errorlevel%

:fail
echo.
echo ERROR: Scaffolding stopped. Review the error above.
popd
exit /b 1
