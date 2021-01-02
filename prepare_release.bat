@setlocal enabledelayedexpansion

@set version=%1

@if "%version%" == "" (
  @echo Version needed^^!
  @exit /b 1
)

@set artifactStagingDirectory=%2

@if not exist %artifactStagingDirectory% (
  @echo Staging directory needed^^!
  @exit /b 1
)

@if not exist %artifactStagingDirectory%\win-x86\RVis (
  @echo Expecting published x86^^!
  @exit /b 1
)

@mkdir %artifactStagingDirectory%\win-x86\RVis\module

@mkdir %artifactStagingDirectory%\win-x86\RVis\module\estimation
@copy UI\module\Estimation\bin\Release\net5.0-windows\win-x86\Estimation.dll %artifactStagingDirectory%\win-x86\RVis\module\estimation\ >nul

@mkdir %artifactStagingDirectory%\win-x86\RVis\module\evidence
@copy UI\module\Evidence\bin\Release\net5.0-windows\win-x86\Evidence.dll %artifactStagingDirectory%\win-x86\RVis\module\evidence\ >nul

@mkdir %artifactStagingDirectory%\win-x86\RVis\module\plot
@copy UI\module\Plot\bin\Release\net5.0-windows\win-x86\Plot.dll %artifactStagingDirectory%\win-x86\RVis\module\plot\ >nul

@mkdir %artifactStagingDirectory%\win-x86\RVis\module\sampling
@copy UI\module\Sampling\bin\Release\net5.0-windows\win-x86\Sampling.dll %artifactStagingDirectory%\win-x86\RVis\module\sampling\ >nul

@mkdir %artifactStagingDirectory%\win-x86\RVis\module\sensitivity
@copy UI\module\Sensitivity\bin\Release\net5.0-windows\win-x86\Sensitivity.dll %artifactStagingDirectory%\win-x86\RVis\module\sensitivity\ >nul

@echo.
@echo Prepared portable %dirName%
@echo.

@dir %dirName% /B /S

@echo.

@if not exist %artifactStagingDirectory%\win-x64\RVis (
  @echo Expecting published x64^^!
  @exit /b 1
)

@mkdir %artifactStagingDirectory%\win-x64\RVis\module

@mkdir %artifactStagingDirectory%\win-x64\RVis\module\estimation
@copy UI\module\Estimation\bin\Release\net5.0-windows\win-x64\Estimation.dll %artifactStagingDirectory%\win-x64\RVis\module\estimation\ >nul

@mkdir %artifactStagingDirectory%\win-x64\RVis\module\evidence
@copy UI\module\Evidence\bin\Release\net5.0-windows\win-x64\Evidence.dll %artifactStagingDirectory%\win-x64\RVis\module\evidence\ >nul

@mkdir %artifactStagingDirectory%\win-x64\RVis\module\plot
@copy UI\module\Plot\bin\Release\net5.0-windows\win-x64\Plot.dll %artifactStagingDirectory%\win-x64\RVis\module\plot\ >nul

@mkdir %artifactStagingDirectory%\win-x64\RVis\module\sampling
@copy UI\module\Sampling\bin\Release\net5.0-windows\win-x64\Sampling.dll %artifactStagingDirectory%\win-x64\RVis\module\sampling\ >nul

@mkdir %artifactStagingDirectory%\win-x64\RVis\module\sensitivity
@copy UI\module\Sensitivity\bin\Release\net5.0-windows\win-x64\Sensitivity.dll %artifactStagingDirectory%\win-x64\RVis\module\sensitivity\ >nul

@echo.
@echo Prepared portable %dirNamex64%
@echo.

@dir %dirNamex64% /B /S

@echo.

@endlocal

@exit /b 0
