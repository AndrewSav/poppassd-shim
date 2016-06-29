# poppassd-shim
## Description
Drop in replacement for poppassd that works with dovecot passdb files instead of pam.

I have an installation of postfix/dovecot that works together with RainLoop web client. I wanted to allow user password change from the web ui. There are a number of password change plug-ins to RainLoop but none worked for me out of the box. Poppassd plugin looked most promising. Unfortunately poppassd works with pam only, and my dovecot installation does not use pam, it uses virtual users in passdb.

This set of scripts replaces standard **Ubuntu** poppassd application with an alternate service that updates dovecot passdb instead of pam.

I only implemented the minimum features I needed for myself, but the code is simple enough to be reused elsewhere.

The caveat is that I never programmed for linux before, so I choose mono, because I'm proficient with .Net. You are warned.

## Set up
Install the following packages:
```
sudo apt-get install build-essential mono-complete poppassd
```
This installs the dev environment but also the standard poppassd. I chose this way because poppassd package wires up to inetd, start on reboot, etc the things that I have very vague idea how to set up properly. So I rely on this package to set them up for me.

As root compile poppassd.c:
```
gcc -o poppassd poppassd.c
```
You might want to edit the constant at the top of poppassd.c if mono is not installed at the standard location in your system. I'm assuming that you do not change the `EXE` constant, if you do, review following instruction accordingly.

As root copy compiled poppassd to /usr/sbin/poppassd to replace the standard one:
```
mv /usr/sbin/poppassd /usr/sbin/poppassd.old
cp poppassd /usr/sbin
```

As root compile poppassd.cs:
```
mcs poppassd.cs -r:Mono.Posix.dll -r:System.Configuration.dll -debug
```
Copy generated executable and symbols file to /usr/sbin and also copy poppassd.exe.config there. You might need to edit the config if doveadm is located in non-standard location. Also change shadowPath setting to point to your dovecot passdb file.
```
cp poppassd.exe /usr/sbin
cp poppassd.exe.mdb /usr/sbin
cp poppassd.exe.config /usr/sbin
```
That's it. You might need to cycle inetd. Use poppassd plugin for RainLoop with default settings and you should be up and running. As with standard poppassd make sure that it's not possible to access the service from elsewhere because passwords are transmitted unencrypted. (So you should really use localhost only and make sure you can't connect to it from anywhere else.)

Note that erros are logged to syslog with "cli:" prefix.
