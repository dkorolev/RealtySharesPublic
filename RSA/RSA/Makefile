.PHONY: all test clean

all: Program.exe

Program.exe:
	fsharpc Program.fs

test: Program.exe
	./test.sh

clean:
	rm -f Program.exe
