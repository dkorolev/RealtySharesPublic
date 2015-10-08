#!/bin/bash

cat <<EOF >input.txt
This is a simple input file to encrypt.
EOF

./Program.exe genkey key_public.xml key_private.xml
./Program.exe encrypt key_public.xml input.txt encrypted.txt
./Program.exe decrypt key_private.xml encrypted.txt output.txt

diff input.txt output.txt && echo OK || echo Fail.
