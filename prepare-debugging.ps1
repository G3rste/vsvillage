Remove-Item "bin/vsdebug" -Force -Recurse; 
New-Item -Path "bin/vsdebug" -Name "vsvillage" -ItemType "directory"; 
Copy-Item -Path "resources/*" -Destination "bin/vsdebug/vsvillage" -Recurse;
Copy-Item -Path "bin/Debug/net7.0/*" -Destination "bin/vsdebug/vsvillage";