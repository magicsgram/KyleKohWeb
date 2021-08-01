wsl rm tempconn.tar.gz
dotnet publish -c Release -r linux-x64 --self-contained=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=true -o ./webpublish
wsl tar -zcvf tempconn.tar.gz webpublish/
wsl rm -rf webpublish/
