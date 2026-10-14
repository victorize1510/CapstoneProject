from pathlib import Path
import re, sys
sys.path.insert(0,str(Path(__file__).parent))
from implement_ui import ASSETS, UI, ROOT, BACKUP, write
from enhance import display_words

for backup in BACKUP.rglob('*.cs'):
    p=ROOT/backup.relative_to(BACKUP)
    text=p.read_text(encoding='utf8')
    original=backup.read_text(encoding='utf-8-sig')
    # Localization must never rewrite interpolated expressions or query identifiers.
    for expression in re.findall(r'\{[^{}]*\}', original):
        changed=display_words(expression)
        if changed!=expression: text=text.replace(changed,expression)
    for literal in re.findall(r'"(?:\\.|[^"\\])*"',original):
        content=literal[1:-1]
        if re.match(r'(?:pet|box|quest|inventory|profile)-',content):
            changed=display_words(content)
            if changed!=content: text=text.replace('"'+changed+'"',literal)
    write(p,text)
print('Verified display changes preserve interpolated code and UI identifiers.')
