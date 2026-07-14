try {
    $connections = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue
    $connections += Get-NetTCPConnection -LocalPort 5001 -ErrorAction SilentlyContinue
    
    foreach ($conn in $connections) {
        $pidToKill = $conn.OwningProcess
        if ($pidToKill -gt 0) {
            Write-Output "Stopping process $pidToKill using port $($conn.LocalPort)"
            Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Output "Ports freed."
} catch {
    Write-Error $_.Exception.Message
}
