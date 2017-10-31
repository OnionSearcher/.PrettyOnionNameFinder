rem 24h full restart
shutdown /r /t 86400
IF NOT ERRORLEVEL shutdown /r /t 0
exit /B 0
