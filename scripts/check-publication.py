"""Check tracked publication files and documentation links without network access."""
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import unquote,urlsplit
import re,subprocess,sys

root=Path(__file__).resolve().parents[1]
result=subprocess.run(['git','ls-files','-z'],cwd=root,capture_output=True,text=True)
if result.returncode or not result.stdout:
 files=[p for p in root.rglob('*') if p.is_file() and not any(s in p.parts for s in ['.git','node_modules','bin','obj','build','outputs','artifacts','runtime','__pycache__'])]
else:files=[root/name for name in result.stdout.split('\0') if name]
errors=[];image_count=0
blocked={'.jks','.keystore','.p12','.pfx','.pem','.key','.apk','.exe','.dll','.pdb'}
patterns=[re.compile(r'gh[pousr]_[A-Za-z0-9]{20,}'),re.compile(r'github_pat_[A-Za-z0-9_]{20,}'),re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),re.compile(r'(?i)[A-Z]:[\\/]+Users[\\/]+(?!Public[\\/]|Default[\\/])[^\\/\r\n ]+')]
def anchors(path):
 text=path.read_text(encoding='utf-8-sig');values=set(re.findall(r'\bid=["\']([^"\']+)',text))
 for heading in re.findall(r'^#{1,6}\s+(.+)$',text,re.M):
  values.add(re.sub(r'[^\w\- ]','',heading.lower()).replace(' ','-'))
 return values
def link(source,target,is_image=False):
 global image_count
 if target.startswith(('http://','https://','mailto:')):return
 target=target.strip('<>');parsed=urlsplit(target);path=(source.parent/unquote(parsed.path)).resolve() if parsed.path else source
 if root not in path.parents and path!=root:errors.append(f'{source.relative_to(root)}: path escapes repository');return
 if not path.is_file():errors.append(f'{source.relative_to(root)}: missing {target}');return
 if parsed.fragment and path.suffix=='.md' and unquote(parsed.fragment) not in anchors(path):errors.append(f'{source.relative_to(root)}: missing anchor {target}')
 if is_image:image_count+=1
class Images(HTMLParser):
 def __init__(self,source):super().__init__();self.source=source
 def handle_starttag(self,tag,attrs):
  attrs=dict(attrs)
  if tag=='img':
   if not attrs.get('alt'):errors.append(f'{self.source.relative_to(root)}: image without alt text')
   link(self.source,attrs.get('src',''),True)
for path in files:
 name=path.relative_to(root)
 if path.suffix.lower() in blocked or 'signing' in name.parts or path.name.startswith('.env'):errors.append(f'Forbidden publication file: {name}')
 if path.suffix.lower() in ['.png','.ico']:continue
 try:text=path.read_text(encoding='utf-8-sig')
 except UnicodeDecodeError:errors.append(f'Unexpected binary file: {name}');continue
 for pattern in patterns:
  if pattern.search(text):errors.append(f'Possible secret or personal path: {name}')
 if path.suffix=='.md':
  for m in re.finditer(r'(!?)\[[^\]]*\]\(([^\s)]+)(?:\s+"[^"]*")?\)',text):link(path,m.group(2),m.group(1)=='!')
  Images(path).feed(text)
for name in ['README.md','README_UK.md','LICENSE','THIRD_PARTY_NOTICES.md','docs/INSTALL.md','docs/images/banner.png','.github/workflows/ci.yml']:
 if not (root/name).is_file():errors.append('Required file missing: '+name)
if errors:
 print('\n'.join(errors));sys.exit(1)
print(f'PASS: {len(files)} publication files; local links/anchors; {image_count} image references; no private keys, installer binaries, tokens or personal paths in tracked source.')
