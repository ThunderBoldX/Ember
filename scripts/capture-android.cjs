'use strict';
const {chromium}=require('playwright');const {pathToFileURL}=require('url');const path=require('path');const fs=require('fs');
(async()=>{
 const root=path.resolve(__dirname,'..'),output=path.join(root,'docs/screenshots');fs.mkdirSync(output,{recursive:true});
 const browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:390,height:920},deviceScaleFactor:2});
 await page.addInitScript(()=>{window.Native={command(){},scan(){},audio(){},load(){return'[]'},save(){},importFile(){},exportFile(){},forgetDevice(){}};});
 await page.goto(pathToFileURL(path.join(root,'android/assets/index.html')).href);
 await page.evaluate(()=>{
  const badge=document.createElement('span');badge.className='pill';badge.textContent='ДЕМОНСТРАЦІЙНІ ДАНІ';document.querySelector('header').append(badge);
  for(const card of document.querySelectorAll('.card')){const note=document.createElement('p');note.textContent='Демонстраційні дані';card.prepend(note);}
  Ember.state({connected:true,profile:2,hasMicrophone:true,savedDevices:[{name:'ELK-BLEDDM · Studio Demo',address:'AA:BB:CC:DD:EE:01',profile:2}],spectrum:Array.from({length:32},(_,i)=>.2+.55*Math.exp(-Math.pow((i-9)/7,2))),message:'Демонстраційна стрічка. Приклад без реального Bluetooth.',brightness:70,speed:50,micSensitivity:35,bpm:120,rms:-18,latency:null,playing:false,packets:0});
 });
 await page.screenshot({path:path.join(output,'android-main.png')});
 await page.locator('.card').nth(1).screenshot({path:path.join(output,'android-color.png')});
 await page.locator('[data-tab="1"]').click();await page.locator('.card').nth(2).screenshot({path:path.join(output,'android-music.png')});
 await page.locator('[data-tab="2"]').click();await page.locator('.card').nth(2).screenshot({path:path.join(output,'android-strip-sound.png')});
 await page.locator('[data-tab="3"]').click();await page.locator('#newEffect').click();await page.locator('#effectName').fill('Evening Gradient · Demo');
 await page.locator('#stepColor').fill('#FF5A1F');await page.locator('#duration').fill('1200');await page.locator('#addStep').click();
 await page.locator('#stepColor').fill('#0066FF');await page.locator('#duration').fill('1800');await page.locator('#transition').selectOption('fade');await page.locator('#trigger').selectOption('musicBeat');await page.locator('#beatCount').fill('4');await page.locator('#addStep').click();
 await page.locator('#editor').screenshot({path:path.join(output,'android-recorder.png')});
 await browser.close();console.log('Captured actual Android WebView assets with labelled fictional data.');
})().catch(e=>{console.error(e);process.exit(1)});
