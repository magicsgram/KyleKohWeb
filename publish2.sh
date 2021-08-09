rm tempconn.tar.gz
dotnet publish -c Release -r linux-x64 --self-contained=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=true -o ./webpublish
XZ_OPT=-9 tar -Jcvf tempconn.tar.xz webpublish/
rm -rf webpublish/
