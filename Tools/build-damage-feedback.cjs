const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const write=(p,s)=>fs.writeFileSync(path.join(root,p),s);
const guid=s=>crypto.createHash('md5').update('ClickerGame.DamageFeedback.'+s).digest('hex');
const font='125cb55b44b24c4393181402bc6200e6';
const materialGuid=guid('material'),soundGuid=guid('sound');
let material=read('Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset').split('--- !u!114')[0];
material=material.replace('Bangers SDF Material','Damage Text Comic').replace(/m_ShaderKeywords:[^\r\n]*/,'m_ShaderKeywords: OUTLINE_ON UNDERLAY_ON')
 .replace('m_Texture: {fileID: 28584486757587946}',`m_Texture: {fileID: 28584486757587946, guid: ${font}, type: 2}`);
for(const [key,value]of Object.entries({_OutlineWidth:0.22,_OutlineSoftness:0,_FaceDilate:0.06,_UnderlayOffsetX:0.7,_UnderlayOffsetY:-0.7,_UnderlayDilate:0.12,_UnderlaySoftness:0})){
 material=material.replace(new RegExp('(- '+key+': )[^\\r\\n]+'),'$1'+value);
}
material=material.replace('- _UnderlayColor: {r: 0, g: 0, b: 0, a: 0.5}','- _UnderlayColor: {r: 0, g: 0, b: 0, a: 1}');
write('Assets/Materials/Damage Text Comic.mat',material);
write('Assets/Materials/Damage Text Comic.mat.meta',`fileFormatVersion: 2\nguid: ${materialGuid}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n`);
const prefabPath='Assets/Prefabs/Efects/Damage Text.prefab';
let prefab=read(prefabPath).replace(/  - component: \{fileID: 2410041566512821184\}\r?\n/,'')
 .replace(/^--- !u!95 &2410041566512821184\r?\n[\s\S]*?(?=^--- !u!)/m,'');
prefab=prefab.replace(/m_fontAsset: \{[^\r\n]+\}/,`m_fontAsset: {fileID: 11400000, guid: ${font}, type: 2}`)
 .replaceAll('{fileID: 5301406559370880690, guid: 87c7276b22e4ae3449a39f465315d554, type: 2}',`{fileID: 2100000, guid: ${materialGuid}, type: 2}`)
 .replace('  lifetime: 1','  lifetime: 0.9').replace(/  moveSpeed:[^\r\n]*\r?\n/,'')
 .replace('m_fontWeight: 400','m_fontWeight: 700').replace('m_fontStyle: 0','m_fontStyle: 1')
 .replace('m_enableExtraPadding: 0','m_enableExtraPadding: 1').replace('m_RaycastTarget: 1','m_RaycastTarget: 0')
 .replace('m_LightProbeUsage: 1','m_LightProbeUsage: 0').replace('m_ReflectionProbeUsage: 1','m_ReflectionProbeUsage: 0')
 .replace('m_MotionVectors: 1','m_MotionVectors: 0');
write(prefabPath,prefab);
// Original synthesized impact: short downward pitch sweep with a dry transient.
// Generated once as an imported clip, never allocated per hit.
const rate=44100,count=Math.floor(rate*0.2),wav=Buffer.alloc(44+count*2);
wav.write('RIFF');wav.writeUInt32LE(wav.length-8,4);wav.write('WAVEfmt ',8);wav.writeUInt32LE(16,16);
wav.writeUInt16LE(1,20);wav.writeUInt16LE(1,22);wav.writeUInt32LE(rate,24);wav.writeUInt32LE(rate*2,28);wav.writeUInt16LE(2,32);wav.writeUInt16LE(16,34);wav.write('data',36);wav.writeUInt32LE(count*2,40);
let phase=0,seed=17;
for(let i=0;i<count;i++){
 const t=i/rate;phase+=2*Math.PI*(55+160*Math.exp(-t*35))/rate;
 seed=(Math.imul(seed,1664525)+1013904223)>>>0;
 const noise=(seed/4294967296)*2-1;
 const attack=Math.min(1,t/0.002),tail=Math.min(1,(count-i)/(rate*0.02));
 const v=attack*tail*(Math.sin(phase)*0.72*Math.exp(-t*22)+noise*0.23*Math.exp(-t*95));
 wav.writeInt16LE(Math.round(Math.max(-1,Math.min(1,v))*32767),44+i*2);
}
const soundPath='Assets/Resources/Harvest Impact.wav';write(soundPath,wav);
write(soundPath+'.meta',`fileFormatVersion: 2\nguid: ${soundGuid}\nAudioImporter:\n  externalObjects: {}\n  serializedVersion: 8\n  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n    preloadAudioData: 1\n  platformSettingOverrides: {}\n  forceToMono: 1\n  normalize: 0\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n`);
const scene='Assets/Scenes/GameScene.unity';let s=read(scene);
if(!s.includes('  hitSound:'))s=s.replace('  hitPoolSize: 15',`  hitPoolSize: 15\n  hitParticleCount: 15\n  hitSound: {fileID: 8300000, guid: ${soundGuid}, type: 3}`);
write(scene,s);
console.log('Damage text: Bangers + shared outline/shadow material; Animator removed; impact clip assigned.');
