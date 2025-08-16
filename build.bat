dotnet build
pause
:: wget https://rules2.clearurls.xyz/data.minify.json -O clearurls.json
copy clearurls.json .\bin\Debug\net9.0-windows\
start .\bin\Debug\net9.0-windows\ClipDump.exe
