const { chromium } = require('playwright');
const path=require('path');const {pathToFileURL}=require('url');const fs=require('fs');
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:390,height:844},deviceScaleFactor:1});let errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{window.Native={command(k,v){window.commands.push([k,v])},scan(){},audio(){},load(){return'[]'},save(){},importFile(){},exportFile(){},forgetDevice(a){window.forgot=a}};window.commands=[];});
 await page.goto(pathToFileURL(path.join(__dirname,'../assets/index.html')).href);
 const assert=(ok,n)=>{if(!ok)throw Error(n);console.log('PASS '+n)};
 for(const tab of [0,1,2]){await page.locator(`[data-tab="${tab}"]`).click();assert(await page.locator('#effects>button').count()===16,'sixteen tiles in tab '+tab);}
 await page.evaluate(()=>Ember.state({connected:false,profile:2,hasMicrophone:true,savedDevices:[{name:'ELK-BLEDDM',address:'AA:BB:CC:DD:EE:FF',profile:2}],spectrum:Array(32).fill(.1),message:'Готово'}));
 assert(await page.locator('#savedDevices .row').count()===1,'saved devices rendered without scanning');
 await page.locator('#savedDevices .row>button').first().click();assert(await page.evaluate(()=>commands.at(-1).join('|'))==='connect|AA:BB:CC:DD:EE:FF','direct saved device reconnect');
 await page.evaluate(()=>Ember.devices([{name:'ELK-BLEDDM',address:'AA:BB:CC:DD:EE:FF',rssi:-53},{name:'Triones',address:'11:22:33:44:55:66',rssi:-64}]));
 assert(await page.locator('#savedDevices').innerText().then(s=>s.includes('Поруч')),'saved online device marked nearby');assert(await page.locator('#devices>button').count()===1,'saved devices are not duplicated in scan results');
 await page.locator('#savedDevices button[aria-label]').click();assert(await page.evaluate(()=>forgot)==='AA:BB:CC:DD:EE:FF','forget action');
 await page.evaluate(()=>Ember.state({connected:true,profile:3,hasMicrophone:false,savedDevices:[],spectrum:[],message:'Підключено · Triones'}));
 assert(await page.locator('[data-tab="2"]').isDisabled(),'unsupported microphone disabled');assert(await page.locator('#effects>button').first().innerText().then(s=>s.includes('Червоний строб')),'Triones native labels match available modes');
 assert(await page.locator('#profile').count()===0,'manual controller selector removed');
 assert(await page.locator('main h1').count()===0,'hero headline removed');
 assert(!(await page.locator('body').innerText()).includes('Керуй RGB-стрічкою через Bluetooth: кольори, ефекти й музика.'),'hero description removed');
 await page.locator('[data-tab="0"]').click();await page.evaluate(()=>commands=[]);await page.locator('#effects>button').first().click();
 assert(await page.evaluate(()=>commands.length===1&&commands[0].join('|')==='mode|135'),'one autonomous tile click sends one mode selection');
 await page.evaluate(()=>Ember.state({connected:true,profile:2,hasMicrophone:true,savedDevices:[{name:'ELK-BLEDDM',address:'AA:BB:CC:DD:EE:FF'}],spectrum:Array(32).fill(.25),message:'Підключено'}));
 await page.locator('[data-tab="2"]').click();const bounds=await page.evaluate(()=>({width:document.documentElement.scrollWidth,view:innerWidth}));assert(bounds.width<=bounds.view,'mobile layout has no horizontal overflow');
 const output=path.join(__dirname,'../../artifacts/screenshots');fs.mkdirSync(output,{recursive:true});await page.screenshot({path:path.join(output,'android-390.png'),fullPage:true});
 await page.setViewportSize({width:760,height:1000});await page.screenshot({path:path.join(output,'android-760.png'),fullPage:true});
 assert(errors.length===0,'no JavaScript errors');await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
