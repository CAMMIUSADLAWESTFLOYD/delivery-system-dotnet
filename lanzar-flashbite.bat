@echo off
REM ============================================================
REM  FlashBite - Lanzador: app + tunel publico sin password
REM  Doble clic y listo. Cierra esta ventana para apagar todo.
REM ============================================================
title FlashBite Delivery - Servidor + Tunel
cd /d "%~dp0"

echo [1/3] Compilando la aplicacion...
dotnet build --nologo -v q
if errorlevel 1 (
    echo ERROR de compilacion. Revisa los mensajes de arriba.
    pause
    exit /b 1
)

echo [2/3] Iniciando FlashBite en http://localhost:5000 ...
start "FlashBite App" /min cmd /c "cd bin\Debug\net10.0 && delivery-system-dotnet.exe"

REM Esperar a que el servidor responda antes de abrir el tunel
timeout /t 8 /nobreak > nul

echo [3/3] Publicando en internet usando Tunnelmole...
echo.
echo ============================================================
echo   Revisa mas abajo la URL que te asigna Tunnelmole
echo   (No tendra contrasena)
echo ============================================================
echo.
echo Deja esta ventana abierta mientras quieras compartir el sitio.
echo.
npx -y tunnelmole 5000

REM Al cerrar el tunel, apagar tambien la app
taskkill /IM delivery-system-dotnet.exe /F > nul 2>&1

