from __future__ import annotations
import json, re, sys, xml.etree.ElementTree as ET
from pathlib import Path
try:
    import yaml
except Exception:
    yaml = None

root = Path(__file__).resolve().parents[1]
errors=[]
results=[]

def files(pattern):
    return [p for p in root.rglob(pattern) if '.git' not in p.parts and 'bin' not in p.parts and 'obj' not in p.parts]

# JSON
json_files=files('*.json')
for p in json_files:
    try: json.loads(p.read_text(encoding='utf-8-sig'))
    except Exception as e: errors.append(f'JSON inválido {p.relative_to(root)}: {e}')
results.append(f'JSON válidos: {len(json_files)}')

# YAML
_yaml=files('*.yml')+files('*.yaml')
for p in _yaml:
    try:
        if yaml is None: raise RuntimeError('PyYAML indisponível')
        yaml.safe_load(p.read_text(encoding='utf-8-sig'))
    except Exception as e: errors.append(f'YAML inválido {p.relative_to(root)}: {e}')
results.append(f'YAML válidos: {len(_yaml)}')

# XML-ish
xml_ext={'.csproj','.props','.targets','.xml','.manifest'}
xml_files=[p for p in root.rglob('*') if p.is_file() and p.suffix.lower() in xml_ext and '.git' not in p.parts and 'bin' not in p.parts and 'obj' not in p.parts]
for p in xml_files:
    try: ET.parse(p)
    except Exception as e: errors.append(f'XML inválido {p.relative_to(root)}: {e}')
results.append(f'XML/projetos válidos: {len(xml_files)}')

# C# lexical delimiter validation, ignoring comments and all common string/char forms.
def strip_csharp(text: str) -> str:
    out=[]; i=0; n=len(text)
    state='code'; raw_quotes=0
    while i<n:
        c=text[i]; nxt=text[i+1] if i+1<n else ''
        if state=='code':
            # raw strings, including interpolated raw strings
            j=i
            while j<n and text[j]=='$': j+=1
            if j<n and text[j]=='"':
                k=j
                while k<n and text[k]=='"': k+=1
                if k-j>=3:
                    raw_quotes=k-j; out.extend(' '*(k-i)); i=k; state='raw'; continue
            if c=='/' and nxt=='/': out.extend('  '); i+=2; state='line'; continue
            if c=='/' and nxt=='*': out.extend('  '); i+=2; state='block'; continue
            if c=='@' and nxt=='"': out.extend('  '); i+=2; state='verbatim'; continue
            if c=='$' and nxt=='@' and i+2<n and text[i+2]=='"': out.extend('   '); i+=3; state='verbatim'; continue
            if c=='@' and nxt=='$' and i+2<n and text[i+2]=='"': out.extend('   '); i+=3; state='verbatim'; continue
            if c=='$' and nxt=='"': out.extend('  '); i+=2; state='string'; continue
            if c=='"': out.append(' '); i+=1; state='string'; continue
            if c=="'": out.append(' '); i+=1; state='char'; continue
            out.append(c); i+=1; continue
        if state=='line':
            if c=='\n': out.append('\n'); state='code'
            else: out.append(' ')
            i+=1; continue
        if state=='block':
            if c=='*' and nxt=='/': out.extend('  '); i+=2; state='code'
            else: out.append('\n' if c=='\n' else ' '); i+=1
            continue
        if state=='string':
            if c=='\\': out.extend('  ' if i+1<n else ' '); i+=2
            elif c=='"': out.append(' '); i+=1; state='code'
            else: out.append('\n' if c=='\n' else ' '); i+=1
            continue
        if state=='char':
            if c=='\\': out.extend('  ' if i+1<n else ' '); i+=2
            elif c=="'": out.append(' '); i+=1; state='code'
            else: out.append('\n' if c=='\n' else ' '); i+=1
            continue
        if state=='verbatim':
            if c=='"' and nxt=='"': out.extend('  '); i+=2
            elif c=='"': out.append(' '); i+=1; state='code'
            else: out.append('\n' if c=='\n' else ' '); i+=1
            continue
        if state=='raw':
            if c=='"':
                k=i
                while k<n and text[k]=='"': k+=1
                if k-i>=raw_quotes:
                    out.extend(' '*(k-i)); i=k; state='code'; continue
            out.append('\n' if c=='\n' else ' '); i+=1
    return ''.join(out)

cs_files=files('*.cs')
pairs={')':'(',']':'[','}':'{'}
for p in cs_files:
    cleaned=strip_csharp(p.read_text(encoding='utf-8-sig'))
    stack=[]
    for pos,c in enumerate(cleaned):
        if c in '([{': stack.append((c,pos))
        elif c in ')]}':
            if not stack or stack[-1][0]!=pairs[c]:
                line=cleaned.count('\n',0,pos)+1; errors.append(f'Delimitador C# inválido {p.relative_to(root)}:{line} ({c})'); break
            stack.pop()
    else:
        if stack:
            c,pos=stack[-1]; line=cleaned.count('\n',0,pos)+1; errors.append(f'Delimitador C# não fechado {p.relative_to(root)}:{line} ({c})')
results.append(f'Arquivos C# com estrutura balanceada: {len(cs_files)}')

# Solution/project references
sln=root/'FlowSentinel.sln'
sln_text=sln.read_text(encoding='utf-8-sig',errors='replace')
project_paths=re.findall(r'Project\("\{[^}]+\}"\) = "[^"]+", "([^"]+\.csproj)"',sln_text)
for rel in project_paths:
    p=root/Path(rel.replace('\\','/'))
    if not p.exists(): errors.append(f'Projeto da solução ausente: {rel}')
results.append(f'Projetos referenciados pela solução: {len(project_paths)}')
project_ref_count=0
for proj in files('*.csproj'):
    tree=ET.parse(proj)
    for node in tree.findall('.//ProjectReference'):
        project_ref_count+=1
        inc=node.attrib.get('Include','').replace('\\','/')
        target=(proj.parent/inc).resolve()
        if not target.exists(): errors.append(f'ProjectReference ausente em {proj.relative_to(root)}: {inc}')
results.append(f'ProjectReference conferidos: {project_ref_count}')

# Specific regression guards
parser_test=(root/'tests/FlowSentinel.Infrastructure.Tests/ExcelSectionedMatrixParserTests.cs').read_text(encoding='utf-8-sig')
if parser_test.count('GenerateAggregateRecords = true') < 3:
    errors.append('Os três testes de agregação não habilitam GenerateAggregateRecords explicitamente.')
if 'Assert.DoesNotContain(records, x => x.Fields.GetValueOrDefault("__recordType") == "Aggregate")' not in parser_test:
    errors.append('Não foi localizada a proteção do padrão sem agregações.')
executor=(root/'src/FlowSentinel.Application/AutomationExecutor.cs').read_text(encoding='utf-8-sig')
for required in ['EvaluateWhileActiveStateAsync','CompletionConditions','PersistenceConditions','CancelPendingDeliveriesAsync(occurrence.Id, action.Id','EpisodeNumber']:
    if required not in executor: errors.append(f'Executor sem elemento obrigatório: {required}')

if 'IncludeFormatting = true' not in parser_test:
    errors.append('O teste de destaque visual não habilita IncludeFormatting explicitamente.')
rule_test=(root/'tests/FlowSentinel.Application.Tests/RuleEngineTests.cs').read_text(encoding='utf-8-sig')
for required in ['DeveAceitarLetraNumeroPalavraOuFraseComoValorDePendencia', 'AGUARDANDO DOCUMENTAÇÃO', '[InlineData("7")]']:
    if required not in rule_test: errors.append(f'Teste genérico de pendência sem elemento obrigatório: {required}')
version=(root/'eng/Version.props').read_text(encoding='utf-8-sig')
if '<VersionPrefix>0.4.2</VersionPrefix>' not in version:
    errors.append('VersionPrefix não está em 0.4.2.')

print('\n'.join(results))
if errors:
    print('\nERROS:', file=sys.stderr)
    print('\n'.join('- '+e for e in errors), file=sys.stderr)
    sys.exit(1)
print('Resultado: APROVADO')
