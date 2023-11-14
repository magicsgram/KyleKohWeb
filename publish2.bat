wsl rm tempconn.tar.xz
dotnet publish -c Release -r linux-x64 --self-contained=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=true -o ./webpublish
wsl XZ_OPT=-9 tar -Jcvf tempconn.tar.xz webpublish/
wsl rm -rf webpublish/
