$modinfo=Get-Content -Path resources\modinfo.json | ConvertFrom-Json
$modid=$modinfo.modid;
$authors=$modinfo.authors -Join '';
$downloadpath="resources/assets/$modid/lang/";
$downloadfile= $downloadpath + 'translations.zip';
Invoke-Webrequest -Uri "https://dl.dropboxusercontent.com/scl/fo/q7u3idxz3edsytki8n6m4/h/$modid-$authors.zip?dl=1&rlkey=mc3xn22a49qwrjp5cmx1he0ay" -OutFile $downloadfile;
Expand-Archive -Force -Path $downloadfile -DestinationPath $downloadpath;
Remove-Item -Path $downloadfile; 