#!/bin/bash

echo Building...
make || exit 1

echo Creating package
rm -rf tmp
mkdir tmp

source module.info
echo -n '<Config>
  <Entry>
    <anyType xmlns:q1="http://www.w3.org/2001/XMLSchema" d1p1:type="q1:string" xmlns:d1p1="http://www.w3.org/2001/XMLSchema-instance">FileName</anyType>
    <anyType xmlns:q1="http://www.w3.org/2001/XMLSchema" d1p1:type="q1:string" xmlns:d1p1="http://www.w3.org/2001/XMLSchema-instance">'"$FileName"'</anyType>
  </Entry>
  <Entry>
    <anyType xmlns:q1="http://www.w3.org/2001/XMLSchema" d1p1:type="q1:string" xmlns:d1p1="http://www.w3.org/2001/XMLSchema-instance">ClassName</anyType>
    <anyType xmlns:q1="http://www.w3.org/2001/XMLSchema" d1p1:type="q1:string" xmlns:d1p1="http://www.w3.org/2001/XMLSchema-instance">'"$ClassName"'</anyType>
  </Entry>
  <Entry>
    <anyType xmlns:q1="http://www.w3.org/2001/XMLSchema" d1p1:type="q1:string" xmlns:d1p1="http://www.w3.org/2001/XMLSchema-instance">Namespace</anyType>
    <anyType xmlns:q1="http://www.w3.org/2001/XMLSchema" d1p1:type="q1:string" xmlns:d1p1="http://www.w3.org/2001/XMLSchema-instance">'"$Namespace"'</anyType>
  </Entry>
</Config>' > tmp/pointer.targets

date +"%s" > tmp/patch.ver

rsync -avr --exclude-from=exclude.files build/module/ tmp --quiet
rm -f build/package.cpkg
cd tmp
zip -r ../build/package.cpkg .
cd ..
rm -rf tmp
