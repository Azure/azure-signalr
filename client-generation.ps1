node -v
if (! $? ) {
	throw "To use Autorest to generate REST client, Node.js is required. Version 10.15.x LTS is preferred. "
}
autorest --info  | Out-Null
if (-not $? ) {
	npm install -g autorest
}
if (Test-Path .\src\Microsoft.Azure.SignalR.Common\RestClients\Generated ) {
	Remove-Item .\src\Microsoft.Azure.SignalR.Common\RestClients\Generated -Force -Recurse
}
autorest --csharp  --v3 src\Microsoft.Azure.SignalR.Common\RestClients\readme.md  
Remove-Item .\src\Microsoft.Azure.SignalR.Common\RestClients\Generated\code-model-v1 