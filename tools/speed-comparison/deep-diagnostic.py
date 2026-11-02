"""Local proposal for one bounded Linux diagnostic; run only after explicit CI approval."""
import collections
import importlib.util
import json
import math
import os
from pathlib import Path
import platform
import re
import shutil
import signal
import subprocess
import tempfile
import time

HERE = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location('same_runner', HERE/'same-runner-comparison.py')
COMMON = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(COMMON)
VERSIONS = ('2.3.3', '2.4.1', '2.5.2')
TARGETS = ('base233', 'head233', 'head241', 'head252')
REFS = ('TUNIT', 'NUNIT', 'MSTEST', 'XUNIT')
PACKAGES = ('Microsoft.Testing.Platform', 'Microsoft.Testing.Platform.MSBuild', 'Microsoft.Testing.Extensions.TrxReport')


def orders(names, cycles):
    """Williams rows: balance positions and within-round preceding participants."""
    n = len(names)
    seed = [0]
    for i in range(1, n):
        seed.append((i+1)//2 if i % 2 else n-i//2)
    rows = [[names[(i+row) % n] for i in seed] for row in range(n)]
    if n % 2:
        rows += [list(reversed(row)) for row in rows]
    for cycle in range(cycles):
        for row in rows:
            yield row if cycle % 2 == 0 else list(reversed(row))


def version_override(tree, version):
    """Keep Platform/MSBuild/TRX on the same release; touch no test body or runtime source."""
    for relative, element, packages in (
        ('Directory.Packages.props', 'PackageVersion', PACKAGES),
        ('tools/speed-comparison/UnifiedTests/UnifiedTests.csproj', 'PackageReference', PACKAGES[1:]),
    ):
        path = tree/relative
        text = path.read_text(encoding='utf-8-sig')
        for package in packages:
            pattern = rf'(<{element} Include="{re.escape(package)}" Version=")[^"]+("\s*/>)'
            text, count = re.subn(pattern, lambda m: m[1]+version+m[2], text)
            if count != 1:
                raise RuntimeError(f'Expected one explicit version for {package} in {relative}')
        path.write_text(text, encoding='utf-8')


def build_one(tree, identifier, label, binaries, results, phases=False):
    project = tree/'tools/speed-comparison/UnifiedTests/UnifiedTests.csproj'
    args = ['dotnet','build',str(project),'-c','Release','-f','net10.0',f'-p:TestFramework={identifier}',
            '--disable-build-servers','-p:UseSharedCompilation=false','-m:1','-v:minimal']
    if phases:
        shutil.copy2(HERE/'PhaseEntryPoint.cs.in', project.parent/'PhaseEntryPoint.cs')
        # These must be project-local: CLI global StartupObject would propagate /main
        # into the referenced Core/Platform/Generator/Analyzers library compilations.
        text=project.read_text(encoding='utf-8-sig')
        properties='''<PropertyGroup>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    <GenerateTestingPlatformApplicationHelper>false</GenerateTestingPlatformApplicationHelper>
    <GenerateSelfRegisteredExtensions>true</GenerateSelfRegisteredExtensions>
    <StartupObject>UnifiedTests.PhaseEntryPoint</StartupObject>
  </PropertyGroup>
'''
        project.write_text(text.replace('</Project>',properties+'</Project>'),encoding='utf-8')
    COMMON.checked(args, tree, results/f'build-{label}.log')
    shutil.copy2(project,results/f'project-{label}.csproj')
    source = project.parent/f'bin/Release-{identifier}/net10.0'
    destination = binaries/label
    shutil.copytree(source,destination)
    config = destination/'UnifiedTests.runtimeconfig.json'
    value = json.loads(config.read_text())
    value['runtimeOptions']['framework']['version'] = COMMON.RUNTIME
    value['runtimeOptions']['rollForward'] = 'Disable'
    COMMON.save(config,value)
    shutil.copy2(config,results/f'runtimeconfig-{label}.json')
    shutil.copy2(destination/'UnifiedTests.deps.json',results/f'deps-{label}.json')
    expected_version={'base233':'2.3.3','head233':'2.3.3','head241':'2.4.1','head252':'2.5.2'}.get(label.removesuffix('-phases'))
    if identifier=='NEXTUNIT' and expected_version:
        libraries=json.loads((destination/'UnifiedTests.deps.json').read_text())['libraries']
        for package in PACKAGES+('Microsoft.Testing.Extensions.TrxReport.Abstractions',):
            selected=[key for key in libraries if key.startswith(package+'/')]
            if selected != [package+'/'+expected_version]:
                raise RuntimeError(f'Unexpected resolved package set for {label}: {selected}')
    assets = json.loads((project.parent/'obj/project.assets.json').read_text())
    COMMON.save(results/f'dependencies-{label}.json',
                {k:{'type':v['type'],'sha512':v.get('sha512')} for k,v in assets['libraries'].items()})
    generated = project.parent/f'obj/Release-{identifier}/net10.0'
    if not phases and (generated/'MicrosoftTestingPlatformEntryPoint.cs').exists():
        shutil.copy2(generated/'MicrosoftTestingPlatformEntryPoint.cs',results/f'entrypoint-{label}.cs')
    if phases:
        shutil.copy2(project.parent/'PhaseEntryPoint.cs',results/f'entrypoint-{label}.cs')
    if (generated/'SelfRegisteredExtensions.cs').exists():
        shutil.copy2(generated/'SelfRegisteredExtensions.cs',results/f'extensions-{label}.cs')
    return destination/'UnifiedTests'


def setup(root, work, results):
    executables, trees, identities = {}, {}, {}
    for label, variable, version in (('base233','BASE_SHA','2.3.3'),('head233','HEAD_SHA','2.3.3'),
                                      ('head241','HEAD_SHA','2.4.1'),('head252','HEAD_SHA','2.5.2')):
        sha = os.environ[variable]
        if not re.fullmatch('[0-9a-f]{40}',sha):
            raise ValueError(f'{variable} must be a full commit SHA')
        actual = COMMON.checked(['git','rev-parse',f'{sha}^{{commit}}'],root,results/f'sha-{label}.txt')
        if actual != sha:
            raise ValueError('Unexpected commit')
        tree = work/label
        COMMON.checked(['git','worktree','add','--detach',str(tree),sha],root,results/f'checkout-{label}.log')
        config = json.loads((tree/'global.json').read_text())
        config['sdk']={'version':COMMON.SDK,'rollForward':'disable','allowPrerelease':False}
        COMMON.save(tree/'global.json',config)
        sdk = COMMON.checked(['dotnet','--version'],tree,results/f'sdk-{label}.txt')
        if sdk != COMMON.SDK:
            raise RuntimeError(f'Unexpected SDK {sdk}')
        version_override(tree,version)
        identities[label]={'source_sha':sha,'testing_package_set':version}
        trees[label]=tree
    runtimes = COMMON.checked(['dotnet','--list-runtimes'],root,results/'runtimes.txt')
    if f'Microsoft.NETCore.App {COMMON.RUNTIME} [' not in runtimes:
        raise RuntimeError('Pinned runtime not installed')
    binaries=work/'binaries'
    for label in TARGETS:
        executables[label]=build_one(trees[label],'NEXTUNIT',label,binaries,results)
    for identifier in REFS:
        executables[identifier]=build_one(trees['head252'],identifier,identifier,binaries,results)
    # Independent snapshots preserve the original generated entry points above.
    for label in TARGETS[1:]:
        executables[label+'-phases']=build_one(trees[label],'NEXTUNIT',label+'-phases',binaries,results,True)
    COMMON.save(results/'case-identities.json',identities)
    COMMON.save(results/'binary-sha256.json',
                {str(p.relative_to(binaries)):__import__('hashlib').sha256(p.read_bytes()).hexdigest()
                 for p in sorted(binaries.rglob('*')) if p.is_file()})
    return executables


def run_phase(name, names, cycles, executable_map, results, instrumented=False):
    directory=results/name
    directory.mkdir()
    (directory/'process-output').mkdir()
    rows=[]
    index=0
    with (directory/'samples.jsonl').open('w') as samples, (directory/'warmup.jsonl').open('w') as warmups:
        def one(label,round_index,position,warmup):
            nonlocal index
            executable=executable_map[label]
            before=COMMON.cpu()
            child=COMMON.child_cpu_ms()
            started=time.perf_counter()
            code,text=COMMON.command([str(executable),'--no-progress','--no-ansi'],executable.parent,60)
            valid=code==0 and all(re.search(rf'\b{k}:\s*{v}\b',text,re.I)
                                    for k,v in (('total',127),('succeeded',127),('failed',0),('skipped',0)))
            elapsed=1000*(time.perf_counter()-started)
            cpu=COMMON.cpu_percent(before,COMMON.cpu())
            index+=1
            filename=f'{index:05}-{label}.txt'
            (directory/'process-output'/filename).write_text(text,encoding='utf-8')
            phases=None
            if instrumented:
                matches=re.findall(r'^NEXTUNIT_PHASES (\{.*\})$',text,re.M)
                valid=valid and len(matches)==1
                if len(matches)==1:
                    phases=json.loads(matches[0])
                    expected={'create_builder_ms','register_extensions_ms','build_ms','run_ms','dispose_ms'}
                    valid=valid and set(phases)==expected and all(
                        isinstance(value,(int,float)) and math.isfinite(value) and value>=0 for value in phases.values())
                    valid=valid and sum(phases.values())<=elapsed+1
            row={'Case':label,'Round':round_index,'Position':position,'Warmup':warmup,
                 'ElapsedMilliseconds':elapsed,'SystemCpuPercent':cpu,'ChildCpuMilliseconds':COMMON.child_cpu_ms()-child,
                 'Executable':str(executable),'ExitCode':code,'Valid':valid,'OutputFile':filename,'Phases':phases}
            stream=warmups if warmup else samples
            stream.write(json.dumps(row)+'\n'); stream.flush()
            if not valid:
                raise RuntimeError(f'Invalid test output: {name}/{filename}')
            if not warmup:
                rows.append(row)
        for pass_index in range(5):
            for label in names:
                one(label,0,pass_index+1,True)
        COMMON.quiet_check(directory,'before-measurement')
        for round_index,order in enumerate(orders(names,cycles),1):
            for position,label in enumerate(order,1):
                one(label,round_index,position,False)
            print(f'{name}: round {round_index}',flush=True)
    COMMON.save(directory/'summary.json',{'counts':dict(collections.Counter(r['Case'] for r in rows)),
                                        'instrumented':instrumented,'note':'Analyze phases separately; no performance gate.'})
    COMMON.quiet_check(directory,'after-measurement')
    (directory/'completed.txt').write_text('complete\n')


def profile(executables,results):
    directory=results/'profiles'; directory.mkdir()
    for label in TARGETS[1:]:
        if time.monotonic()>=COMMON.DEADLINE:
            raise RuntimeError('Internal diagnostic budget exhausted before profiles')
        executable=executables[label]
        trace=directory/f'{label}.nettrace'
        env=os.environ.copy()
        env.update(DOTNET_EnableEventPipe='1',DOTNET_EventPipeOutputPath=str(trace),
                   DOTNET_EventPipeConfig='Microsoft-Windows-DotNETRuntime:18:5',DOTNET_EventPipeCircularMB='16')
        process=subprocess.Popen([str(executable),'--no-progress','--no-ansi'],cwd=executable.parent,env=env,
                                 stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,start_new_session=True)
        try:
            stdout,stderr=process.communicate(timeout=min(60,max(1,COMMON.DEADLINE-time.monotonic())))
        except subprocess.TimeoutExpired:
            os.killpg(process.pid,signal.SIGKILL)
            stdout,stderr=process.communicate()
            (directory/f'{label}.txt').write_text(stdout+stderr,encoding='utf-8')
            raise RuntimeError(f'Profile timed out: {label}') from None
        text=stdout+stderr
        (directory/f'{label}.txt').write_text(text,encoding='utf-8')
        valid=process.returncode==0 and all(re.search(rf'\b{k}:\s*{v}\b',text,re.I)
                                              for k,v in (('total',127),('succeeded',127),('failed',0),('skipped',0)))
        if not valid or not trace.exists() or trace.stat().st_size==0:
            raise RuntimeError(f'Profile failed: {label}')
    COMMON.save(directory/'method.json',{'provider':'Microsoft-Windows-DotNETRuntime','keywords_hex':'18',
                                       'meaning':'JIT + loader events; separate diagnostic launches, excluded from timings',
                                       'new_tool_installation':False})


def main():
    root=HERE.parents[1]
    results=root/'same-runner-results'; results.mkdir()
    status={'status':'incomplete','mode':'deep','versions':VERSIONS}
    try:
        if platform.system()!='Linux' or platform.machine()!='x86_64':
            raise RuntimeError('Linux x64 only; do not measure the active Windows PC')
        COMMON.save(results/'environment.json',{'OS':platform.platform(),'CPUCount':os.cpu_count(),
                                               'CPUInfo':Path('/proc/cpuinfo').read_text(),'SDK':COMMON.SDK,'Runtime':COMMON.RUNTIME})
        work=Path(tempfile.mkdtemp(prefix='same-runner-deep-',dir=os.environ['RUNNER_TEMP']))
        executables=setup(root,work,results)
        run_phase('control',TARGETS+REFS,4,executables,results)
        aa={'head252-A':executables['head252'],'head252-B':executables['head252']}
        run_phase('aa',tuple(aa)+REFS,2,{**executables,**aa},results)
        phases=tuple(label+'-phases' for label in TARGETS[1:])
        run_phase('phases',phases,4,executables,results,True)
        profile(executables,results)
        status['status']='measured; manual review required; no history publication or performance gate'
    except Exception as error:
        status['error']=str(error); raise
    finally:
        COMMON.save(results/'status.json',status)


if __name__=='__main__':
    main()
