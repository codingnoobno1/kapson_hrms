@echo off
rem Double-click to rebuild and redeploy Kapson HRMS to IIS.
rem Pass options through, e.g.:  deploy.cmd -Pull -Apk
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1" %*
