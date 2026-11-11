import {readFile,writeFile} from 'node:fs/promises';
const file=p=>new URL('./dist/'+p,import.meta.url);
let html=await readFile(file('index.html'),'utf8');
html=html.replace('<link rel="stylesheet" href="style.css">','<style>'+await readFile(file('style.css'),'utf8')+'</style>');
let scripts='';
for(const name of ['data.js','logic.js','app.js']){
  html=html.replace('<script defer src="'+name+'"></script>','');
  scripts+='<script>'+(await readFile(file(name),'utf8')).replace(/<\/script/gi,'<\\/script')+'</script>';
}
html=html.replace('</body>',scripts+'</body>');
await writeFile(file('Erigone-Release-Desk.html'),html,'utf8');
console.log('Portable HTML ready: dist/Erigone-Release-Desk.html');
