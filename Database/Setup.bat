@echo off
REM ---------------------------------------------------------------
REM  Creates the PoultryERP database.   Usage:
REM     Setup.bat                      (uses .\SQLEXPRESS, Windows login)
REM     Setup.bat MYSERVER\SQL2019     (named server)
REM     Setup.bat .\SQLEXPRESS demo    (also loads demo farm data)
REM  Existing database (installed before broilers): run only 05_Broiler.sql
REM  Requires sqlcmd (installed with SQL Server / SSMS).
REM ---------------------------------------------------------------
setlocal
set SERVER=%~1
if "%SERVER%"=="" set SERVER=.\SQLEXPRESS
cd /d "%~dp0"
echo Creating database on %SERVER% ...
sqlcmd -S "%SERVER%" -E -b -i 01_Schema.sql || goto :err
sqlcmd -S "%SERVER%" -E -b -i 02_Views_Functions.sql || goto :err
sqlcmd -S "%SERVER%" -E -b -i 03_Seed.sql || goto :err
sqlcmd -S "%SERVER%" -E -b -i 05_Broiler.sql || goto :err
if /I "%~2"=="demo" (
  echo Loading demo data ...
  sqlcmd -S "%SERVER%" -E -b -i 04_DemoData.sql || goto :err
  sqlcmd -S "%SERVER%" -E -b -i 06_BroilerDemo.sql || goto :err
)
echo.
echo Done. Sign in with  admin / Admin@123  (you will be asked to change it).
goto :eof
:err
echo.
echo *** Setup failed - see the message above. ***
exit /b 1
