//gcc -o poppassd poppassd.c
#include <stdio.h>
#include <unistd.h>

#define CMD "/usr/bin/cli"
#define EXE "/usr/sbin/poppassd.exe"

main(int argc, char *argv[]) {

  char* newarg[4];
	newarg[0] = CMD;
	newarg[1] = "--debug";
	newarg[2] = EXE;
	newarg[3] = NULL;
  if (execv(CMD, newarg) == 0) {
    return 1;
  }
  return 0;
}
