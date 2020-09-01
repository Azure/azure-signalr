node -v
if(! $? ){
	throw "To use Autorest to generate REST client, Node.js is required. Version 10.15.x LTS is preferred. "
}
autorest --info  | Out-Null
if(-not $? ){
	npm install -g autorest
}
autorest --csharp  --v3 swagger/readme.md   
# please note that an unused file 'code-model-v1' is also generated, avoid uploading it to repos.