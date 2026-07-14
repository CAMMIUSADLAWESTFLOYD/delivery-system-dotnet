try {
    $conn = New-Object System.Data.SqlClient.SqlConnection("Server=localhost;Database=FlashBiteDB;Trusted_Connection=True;TrustServerCertificate=True")
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TOP 1 Codigo, FechaCreacion FROM CodigosVerificacion ORDER BY FechaCreacion DESC"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Output "OTP_CODE:$($reader['Codigo'])"
    }
    $conn.Close()
} catch {
    Write-Error $_.Exception.Message
}
