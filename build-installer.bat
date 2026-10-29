@echo off
title Gerador de Instalador - Karla Borges Centro de Estetica
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"
pause

