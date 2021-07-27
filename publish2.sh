rm tempconn.tar.gz
dotnet publish -c Release -r linux-x64 --self-contained=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=true -o ./webpublish
tar -zcvf tempconn.tar.gz webpublish/
rm -rf webpublish/
