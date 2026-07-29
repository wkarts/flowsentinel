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
program=(root/'src/FlowSentinel.Desktop/Program.cs').read_text(encoding='utf-8-sig')
for required in ['private static void Run(string[] args)', 'RunStartupStep(', 'WaitWithMessagePump(', 'DatabaseStartupTimeout', 'System.Windows.Forms.Application.Run(context)']:
    if required not in program: errors.append(f'Inicialização responsiva sem elemento obrigatório: {required}')
if 'RunAsync(args).GetAwaiter().GetResult()' in program:
    errors.append('Program ainda inicia o WinForms pelo fluxo assíncrono antigo.')
if program.find('.InitializeAsync(cancellationToken)') > program.find('host.StartAsync(cancellationToken)'):
    errors.append('O host está sendo iniciado antes da inicialização explícita do banco.')
if 'TimeSpan.MinValue' in program:
    errors.append('Program ainda usa TimeSpan.MinValue no temporizador do splash.')
for required in ['TimeSpan? lastProgressRefresh = null', 'ShouldRefreshSplashProgress', 'var elapsed = watch.Elapsed', 'SplashProgressRefreshInterval']:
    if required not in program: errors.append(f'Correção do temporizador sem elemento obrigatório: {required}')
startup_tests=(root/'tests/FlowSentinel.Desktop.Tests/ProgramStartupTimingTests.cs').read_text(encoding='utf-8-sig')
for required in ['PrimeiraAtualizacaoDoSplashNaoDeveSubtrairTimeSpanMinValue', 'AtualizacaoDoSplashDeveRespeitarIntervaloDeDuzentosECinquentaMilissegundos', 'AtualizacaoInicialDeveAceitarDuracaoMaximaSemOverflow']:
    if required not in startup_tests: errors.append(f'Teste de regressão do temporizador ausente: {required}')

splash=(root/'src/FlowSentinel.Desktop/SplashForm.cs').read_text(encoding='utf-8-sig')
for required in ['LoadDeveloperLogoForDarkBackground', 'UpdateElapsed', 'Tempo decorrido']:
    if required not in splash: errors.append(f'Splash sem elemento obrigatório: {required}')
csproj=(root/'src/FlowSentinel.Desktop/FlowSentinel.Desktop.csproj').read_text(encoding='utf-8-sig')
if 'WWSoftwaresDeveloperLogoWhite.png' not in csproj:
    errors.append('Logo branca não está incluída no projeto Desktop.')
if not (root/'src/FlowSentinel.Desktop/Assets/WWSoftwaresDeveloperLogoWhite.png').exists():
    errors.append('Ativo da logo branca não foi localizado.')

logging=(root/'src/FlowSentinel.Infrastructure/Logging.cs').read_text(encoding='utf-8-sig')
for required in ['AutoFlush = false', 'InformationFlushInterval', 'Microsoft.EntityFrameworkCore', 'System.Net.Http.HttpClient']:
    if required not in logging: errors.append(f'Logging otimizado sem elemento obrigatório: {required}')

executor=(root/'src/FlowSentinel.Application/AutomationExecutor.cs').read_text(encoding='utf-8-sig')
for required in ['GetOpenOccurrencesAsync', 'GetActionScheduleStatesAsync', 'MarkOpenOccurrencesEvaluatedAsync', 'openOccurrences.TryGetValue', 'actionStates.TryGetValue']:
    if required not in executor: errors.append(f'Executor otimizado sem elemento obrigatório: {required}')
if 'var occurrence = await _store.GetOpenOccurrenceAsync' in executor:
    errors.append('Executor ainda consulta uma ocorrência por registro.')

dependency=(root/'src/FlowSentinel.Infrastructure/DependencyInjection.cs').read_text(encoding='utf-8-sig')
if 'Default Timeout=15' not in dependency:
    errors.append('Conexões SQLite sem timeout de contenção configurado.')

store=(root/'src/FlowSentinel.Infrastructure/Persistence/FlowStore.cs').read_text(encoding='utf-8-sig')
for required in ['private const int CurrentStorageVersion = 6', 'NormalizeLegacyDateTimeColumnsAsync', 'ExecuteDeleteAsync', 'OccurrenceHeartbeatInterval', 'ExecuteUpdateAsync']:
    if required not in store: errors.append(f'Persistência otimizada sem elemento obrigatório: {required}')
if 'var occurrences = await context.Occurrences.ToArrayAsync' in store[store.find('NormalizeStoredDateTimesAsync'):store.find('UpgradeLegacyWorkbookDefinition')]:
    errors.append('Migração ainda carrega todas as ocorrências para regravação.')

workers=(root/'src/FlowSentinel.Application/Workers.cs').read_text(encoding='utf-8-sig')
if workers.count('InitialStartupDelay') < 4:
    errors.append('Workers não possuem atraso inicial controlado nas duas rotinas.')

tests=(root/'tests/FlowSentinel.Infrastructure.Tests/FlowStoreSqliteTests.cs').read_text(encoding='utf-8-sig')
for required in ['AtualizacaoDeveRemoverSomenteEstadosInativosSemHistorico', 'AtualizacaoDeAvaliacaoDasOcorrenciasDeveSerAgrupadaPorJanela', 'Assert.Equal(6L']:
    if required not in tests: errors.append(f'Testes de persistência sem proteção obrigatória: {required}')
pending_tests=(root/'tests/FlowSentinel.Infrastructure.Tests/AutomationExecutorPendingTests.cs').read_text(encoding='utf-8-sig')
if 'NaoDevePersistirEstadoDeAcaoDeMudancaEnquantoRegistroPermanecerInalterado' not in pending_tests:
    errors.append('Teste de regressão contra estados redundantes não foi localizado.')

version=(root/'eng/Version.props').read_text(encoding='utf-8-sig')
if '<VersionPrefix>0.4.4</VersionPrefix>' not in version:
    errors.append('VersionPrefix não está em 0.4.4.')

print('\n'.join(results))
if errors:
    print('\nERROS:', file=sys.stderr)
    print('\n'.join('- '+e for e in errors), file=sys.stderr)
    sys.exit(1)
print('Resultado: APROVADO')
