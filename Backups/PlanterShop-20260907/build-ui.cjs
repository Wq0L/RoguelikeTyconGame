const fs = require('node:fs');
const assert = require('node:assert/strict');
const file = 'Assets/Scenes/GameScene.unity';
const original = fs.readFileSync(file, 'utf8');
assert.equal(original, fs.readFileSync('Backups/PlanterShop-20260907/GameScene.before.unity', 'utf8'), 'Scene changed since backup');
let existing = original.replace(/\r\n/g, '\n').split(/(?=^--- !u!)/m);
const tmpTemplate = existing.find(b => b.includes('guid: f4688fdb7df04437aeb418b961361dc5'));
const buttonTemplate = existing.find(b => b.includes('guid: 4e29b1a8efbd4b44bb3f3716e73f07ff'));
const guid = p => fs.readFileSync(p + '.meta','utf8').match(/^guid: (\w+)/m)[1];
const art = 'Assets/3D Assets/AI/UIs/';
const sprite = name => `{fileID: 21300000, guid: ${guid(art+name+'.png')}, type: 3}`;
const nil = '{fileID: 0}';
let next = 7100000000;
const id = () => next++;
const nodes=[];
const color = (r,g,b,a=1) => `{r: ${r}, g: ${g}, b: ${b}, a: ${a}}`;
const dark=color(.055,.09,.13), white=color(.9,.96,1), ink=color(.06,.13,.18), cyan=color(.32,.85,.9);
function node(name,parent,x,y,w,h,anchor=[.5,.5],pivot=[.5,.5]){
 const n={go:id(),rect:id(),name,parent,x,y,w,h,anchor,pivot,children:[],components:[],active:1};
 if(parent && typeof parent==='object') parent.children.push(n.rect);
 nodes.push(n); return n;
}
const common = go => `  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: ${nil}\n  m_PrefabInstance: ${nil}\n  m_PrefabAsset: ${nil}\n  m_GameObject: {fileID: ${go}}\n`;
function mono(n,guid,body){const component=id();n.components.push({id:component,text:`--- !u!114 &${component}\nMonoBehaviour:\n${common(n.go)}  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ${guid}, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n${body}`});return component;}
function canvas(n){if(n.canvas)return;n.canvas=id();n.components.push({id:n.canvas,text:`--- !u!222 &${n.canvas}\nCanvasRenderer:\n${common(n.go)}  m_CullTransparentMesh: 1\n`});}
function image(n,sp=null,col=white,raycast=0){canvas(n);return mono(n,'fe87c0e1cc204ed48ad3b37840f39efc',`  m_Material: ${nil}\n  m_Color: ${col}\n  m_RaycastTarget: ${raycast}\n  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_Sprite: ${sp||nil}\n  m_Type: 0\n  m_PreserveAspect: 0\n  m_FillCenter: 1\n  m_FillMethod: 4\n  m_FillAmount: 1\n  m_FillClockwise: 1\n  m_FillOrigin: 0\n  m_UseSpriteMesh: 0\n  m_PixelsPerUnitMultiplier: 1\n`);}
function text(n,label,size=22,col=white,align=2){canvas(n);const component=id();let b=tmpTemplate.replace(/^--- !u!114 &\d+/,`--- !u!114 &${component}`).replace(/m_GameObject: \{fileID: \d+\}/,`m_GameObject: {fileID: ${n.go}}`);
 const fields={m_text:JSON.stringify(label),m_RaycastTarget:0,m_fontAsset:'{fileID: 11400000, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}',m_sharedMaterial:'{fileID: 2180264, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}',m_fontColor:col,m_fontSize:size,m_fontSizeBase:size,m_enableAutoSizing:0,m_fontSizeMin:size,m_fontSizeMax:size,m_HorizontalAlignment:align,m_VerticalAlignment:512,m_TextWrappingMode:1,m_overflowMode:0};
 for(const [key,v]of Object.entries(fields))b=b.replace(new RegExp(`^  ${key}: .*`,'m'),`  ${key}: ${v}`);
 n.components.push({id:component,text:b});return component;
}
function group(n,alpha=1){const component=id();n.components.push({id:component,text:`--- !u!225 &${component}\nCanvasGroup:\n${common(n.go)}  m_Enabled: 1\n  m_Alpha: ${alpha}\n  m_Interactable: ${alpha?1:0}\n  m_BlocksRaycasts: ${alpha?1:0}\n  m_IgnoreParentGroups: 0\n`});return component;}
function button(n,img){const component=id();let b=buttonTemplate.replace(/^--- !u!114 &\d+/,`--- !u!114 &${component}`).replace(/m_GameObject: \{fileID: \d+\}/,`m_GameObject: {fileID: ${n.go}}`).replace(/m_TargetGraphic: .*/,`m_TargetGraphic: {fileID: ${img}}`).replace(/m_Transition: \d+/,'m_Transition: 1').replace(/m_Mode: \d+/,'m_Mode: 0');
 b=b.replace(/(m_HighlightedSprite|m_PressedSprite|m_SelectedSprite|m_DisabledSprite): .*/g,`$1: ${nil}`);
 b=b.replace(/  m_OnClick:[\s\S]*/, '  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n');n.components.push({id:component,text:b});return component;}
const root=node('Planter Shop UI',586766173,0,235,1500,390,[.5,0]);
image(node('Dark Backing',root,0,0,1460,350),null,dark,1);
image(node('Panel Frame',root,0,0,1500,390),sprite('alt full panel'),color(1,1,1));
const heading=text(node('Heading',root,-520,146,330,32),'PLANTER LAB',27,cyan,1);
const summary=text(node('Hint',root,10,146,700,30),'Choose a planter to inspect its production and price.',20,white,1);
const backN=node('Back Button',root,595,146,212,42);const backImg=image(backN,null,color(.13,.25,.31),1);const back=button(backN,backImg);text(node('Label',backN,0,0,200,35),'BACK TO LIST',18);backN.active=0;
const viewport=node('Cards Viewport',root,0,-21,1420,266);
mono(viewport,guid('Library/PackageCache/com.unity.ugui@8ccc29d23a79/Runtime/UGUI/UI/Core/RectMask2D.cs'),'  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n');
const sizes=['1x1','1x2','1x3','2x2','2x3'];
const crops=[[61,57,39,37],[47,51,67,49],[34,45,93,61],[39,41,83,69],[26,35,109,82]];
const cards=[];
for(let i=0;i<sizes.length;i++){
 const sz=sizes[i],path=`Assets/ScriptableObjects/Planters/GrassPlanter ${sz}.asset`;
 const data=fs.readFileSync(path,'utf8');const cost=+data.match(/  cost: (\d+)/)[1];const type=['Stone','Iron','Gold'][+data.match(/  costType: (\d+)/)[1]];
 const n=node(`Planter ${sz}`,viewport,(i-2)*260,0,228,252);const bg=image(n,sprite('saksı background'),color(1,1,1),1);const btn=button(n,bg),cg=group(n);
 text(node('Name',n,0,88,190,32),`PLANTER ${sz}`,23,ink);
 const [x,y,w,h]=crops[i];const scale=Math.min(166/w,124/h);const cx=x+w/2,cy=y+h/2;
 image(node('Planter Icon',n,(80-cx)*scale,(cy-64)*scale,160*scale,128*scale),sprite(`CAN_${sz} saksı`),color(1,1,1));
 const price=text(node('Price',n,0,-87,190,28),`${cost} ${type}`,21,ink);
 cards.push({data:guid(path),rect:n.rect,group:cg,button:btn,background:bg,price});
}
const detail=node('Selected Planter Details',root,145,-21,1050,264);const details=group(detail,0);
image(node('Divider',detail,-555,0,2,238),null,cyan);
const stats=text(node('Stats',detail,-329,40,385,160),'Select a planter',22,white,1);
const description=text(node('Description',detail,187,38,595,180),'Production details',20,white,1);
image(node('Detail Separator',detail,-108,30,1,184),null,color(.18,.3,.36));
const status=text(node('Balance and Feedback',detail,-260,-98,480,42),'',19,cyan,1);
const buyN=node('Buy Button',detail,280,-96,380,53);const buyImg=image(buyN,null,color(.12,.54,.56),1),buy=button(buyN,buyImg);
const buyLabel=text(node('Label',buyN,0,0,355,44),'BUY',23,white);
mono(root,'079227af456a4e3cbb659218f4ad9a92',`  cards:\n${cards.map(c=>`  - data: {fileID: 11400000, guid: ${c.data}, type: 2}\n    rect: {fileID: ${c.rect}}\n    group: {fileID: ${c.group}}\n    button: {fileID: ${c.button}}\n    background: {fileID: ${c.background}}\n    price: {fileID: ${c.price}}`).join('\n')}\n  normalCard: ${sprite('saksı background')}\n  selectedCard: ${sprite('seçili saksı background')}\n  pressedCard: ${sprite('basılı saksı background')}\n  details: {fileID: ${details}}\n  heading: {fileID: ${heading}}\n  summary: {fileID: ${summary}}\n  stats: {fileID: ${stats}}\n  description: {fileID: ${description}}\n  status: {fileID: ${status}}\n  buyLabel: {fileID: ${buyLabel}}\n  buyButton: {fileID: ${buy}}\n  backButton: {fileID: ${back}}\n  transitionDuration: 0.4\n  selectedX: -570\n`);
function serialize(n){return `--- !u!1 &${n.go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: ${nil}\n  m_PrefabInstance: ${nil}\n  m_PrefabAsset: ${nil}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: ${n.rect}}\n${n.components.map(c=>`  - component: {fileID: ${c.id}}\n`).join('')}  m_Layer: 5\n  m_Name: ${n.name}\n  m_TagString: Untagged\n  m_Icon: ${nil}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: ${n.active}\n--- !u!224 &${n.rect}\nRectTransform:\n${common(n.go)}  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:${n.children.length?'\n'+n.children.map(c=>`  - {fileID: ${c}}\n`).join(''):' []\n'}  m_Father: {fileID: ${n.parent.rect||n.parent}}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: {x: ${n.anchor[0]}, y: ${n.anchor[1]}}\n  m_AnchorMax: {x: ${n.anchor[0]}, y: ${n.anchor[1]}}\n  m_AnchoredPosition: {x: ${n.x}, y: ${n.y}}\n  m_SizeDelta: {x: ${n.w}, y: ${n.h}}\n  m_Pivot: {x: ${n.pivot[0]}, y: ${n.pivot[1]}}\n${n.components.map(c=>c.text).join('')}`;}
// Preserve the existing sell button and the old purchase buttons as inactive backups.
const oldCards=new Set(['1387955596','767219666','535644525','1235125643','394775269']);
existing=existing.map(b=>{
 if(b.startsWith('--- !u!224 &586766173\n'))return b.replace('  m_Father:',`  - {fileID: ${root.rect}}\n  m_Father:`);
 if([...oldCards].some(go=>b.startsWith(`--- !u!1 &${go}\n`)))return b.replace('  m_IsActive: 1','  m_IsActive: 0');
 return b;
});
const result=existing.join('')+nodes.map(serialize).join('');
const allIds=[...result.matchAll(/^--- !u!\d+ &(-?\d+)/gm)].map(m=>m[1]);assert.equal(new Set(allIds).size,allIds.length);
const idSet=new Set(allIds);
for(const n of nodes)for(const m of serialize(n).matchAll(/\{fileID: (710\d+)\}/g))assert.ok(idSet.has(m[1]),'Dangling UI reference');
fs.writeFileSync(file,result);
fs.writeFileSync('Backups/PlanterShop-20260907/ui-manifest.json',JSON.stringify({root:root.go,nodes:nodes.length,cards,files:[file,'Assets/Scripts/UI/PlanterShopPanelUI.cs']},null,2));
console.log(`Saved ${nodes.length} editable UI objects under Placment Shop Panel / Planter Shop UI; ${cards.length} cards linked.`);
