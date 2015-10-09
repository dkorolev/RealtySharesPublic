#!/bin/bash

cat <<EOF >input.txt
This is a simple input file to encrypt.
EOF

mono Program.exe genkey key_public.xml key_private.xml
mono Program.exe encrypt key_public.xml input.txt encrypted.txt
mono Program.exe decrypt key_private.xml encrypted.txt output.txt

diff input.txt output.txt && echo OK || echo Fail.
