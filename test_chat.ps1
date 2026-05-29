$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$url = "http://localhost:5231/api/chat"

function Test-Chat($msg) {
    Write-Host "`n--- Testing: $msg ---"
    $bodyObj = @{
        history = @()
        message = $msg
    }
    $body = ConvertTo-Json $bodyObj -Depth 5 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    
    try {
        $request = [System.Net.WebRequest]::Create($url)
        $request.Method = "POST"
        $request.ContentType = "application/json; charset=utf-8"
        $request.GetRequestStream().Write($bytes, 0, $bytes.Length)
        
        $response = $request.GetResponse()
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        $respBody = $reader.ReadToEnd()
        $respJson = $respBody | ConvertFrom-Json
        Write-Host "Bot Reply: $($respJson.reply)" -ForegroundColor Green
    } catch {
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            Write-Host "Error Response: $($reader.ReadToEnd())" -ForegroundColor Red
        } else {
            Write-Host "Error: $_" -ForegroundColor Red
        }
    }
    Start-Sleep -Seconds 6
}

Test-Chat "Có phim hành động nào đang chiếu không?"
Test-Chat "Giá vé bao nhiêu?"
Test-Chat "Chính sách hoàn vé thế nào?"
Test-Chat "Xin chào"
Test-Chat "thời tiết hôm nay"
