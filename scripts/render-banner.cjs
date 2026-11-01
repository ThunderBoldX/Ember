const {chromium}=require('playwright');const fs=require('fs');const path=require('path');const {pathToFileURL}=require('url');
(async()=>{const root=path.resolve(__dirname,'..'),images=path.join(root,'docs/images');const logo=fs.readFileSync(path.join(images,'logo.png')).toString('base64');
 const bars=Array.from({length:48},(_,i)=>{let h=30+130*Math.pow(Math.sin(i*.23),2)+45*Math.cos(i*.59)**2;return `<rect x="${815+i*14}" y="${336-h}" width="7" height="${h}" rx="3" fill="url(#bar)" opacity="${.35+.5*Math.sin(i*.11)**2}"/>`;}).join('');
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="520" viewBox="0 0 1600 520" role="img" aria-label="Ember — Bluetooth RGB control for Windows and Android">
 <defs><radialGradient id="bg"><stop stop-color="#3b160a"/><stop offset="1" stop-color="#080808"/></radialGradient><linearGradient id="bar" x2="0" y2="1"><stop stop-color="#ff783b"/><stop offset="1" stop-color="#ba350b"/></linearGradient><filter id="glow"><feGaussianBlur stdDeviation="9"/></filter></defs>
 <rect width="1600" height="520" fill="#080808"/><ellipse cx="1000" cy="280" rx="900" ry="550" fill="url(#bg)"/>
 <path d="M -180 525 Q 800 180 1780 525" fill="none" stroke="#ff5a1f" stroke-width="8" opacity=".45" filter="url(#glow)"/><path d="M -180 525 Q 800 180 1780 525" fill="none" stroke="#ff5a1f" opacity=".7"/>
 <image href="data:image/png;base64,${logo}" x="55" y="80" width="150" height="150"/>
 <text x="222" y="214" fill="#fafafa" font-family="Segoe UI,Arial,sans-serif" font-size="150" font-weight="700" letter-spacing="8">EMBER</text>
 <text x="84" y="314" fill="#c3c0be" font-family="Segoe UI,Arial,sans-serif" font-size="35" letter-spacing="3">RGB CONTROL · MUSIC · EFFECTS</text>
 <rect x="1108" y="36" width="408" height="62" rx="31" fill="#17100c" stroke="#ff5a1f40"/>
 <text x="1140" y="76" fill="#ff763c" font-family="Segoe UI,Arial,sans-serif" font-size="23" letter-spacing="4">WINDOWS + ANDROID</text>
 ${bars}<rect x="1" y="1" width="1598" height="518" fill="none" stroke="#ffffff12"/>
 </svg>`;
 fs.writeFileSync(path.join(images,'banner.svg'),svg);const browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:1600,height:520},deviceScaleFactor:1});await page.goto(pathToFileURL(path.join(images,'banner.svg')).href);await page.screenshot({path:path.join(images,'banner.png')});await browser.close();console.log('Rendered Ember vector banner.');})();
