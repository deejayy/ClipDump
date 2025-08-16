dotnet publish
pause
wget https://rules2.clearurls.xyz/data.minify.json -O clearurls.json
copy clearurls.json .\bin\Release\net9.0-windows\publish\
::start .\bin\Release\net9.0-windows\publish\ClipDump.exe
