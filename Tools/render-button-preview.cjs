// Render authored SVG controls offline; no browser or live game state involved.
const fs=require('fs'),vm=require('vm'),path=require('path');
const root=path.resolve(__dirname,'..');
const folder=path.join(root,'Docs/UI-Button-Preview');
const html=fs.readFileSync(path.join(folder,'index.html'),'utf8');
const controls=[];
const elements={scene:{append:b=>controls.push(b)},new:{},status:{}};
const document={createElement:()=>({setAttribute(){}}),getElementById:id=>elements[id],
 querySelectorAll:()=>[],fonts:{ready:Promise.resolve()},body:{dataset:{}}};
vm.runInNewContext(html.match(/<script>([\s\S]*?)<\/script>/)[1],{document});
fs.writeFileSync(path.join(folder,'controls.json'),JSON.stringify(controls.map(b=>({className:b.className,svg:b.innerHTML}))));
