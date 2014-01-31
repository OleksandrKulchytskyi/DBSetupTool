@echo off
del /F .\buildLog.txt

call %windir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe .\DBSetup.sln /property:Configuration=Release >buildLog.txt
