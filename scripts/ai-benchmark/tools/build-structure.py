"""Generate pinned, code-only AST metadata. Usage: python build-structure.py FLASK_GIT CODE_JSON OUTPUT_JSON"""
import ast,json,subprocess,sys
from pathlib import Path
repo,source,output=sys.argv[1:]
code=json.loads(Path(source).read_text(encoding='utf-8'));records=[]
for file in sorted({p['file'] for p in code['packets']}):
 for side,sha in [('before',code['baseSha']),('after',code['headSha'])]:
  raw=subprocess.check_output(['git','-C',repo,'show',f'{sha}:{file}']).decode('utf-8')
  lines=raw.splitlines();tree=ast.parse(raw); functions=[];docs=[];statements=[]
  def visit(node,parents):
   scope=parents
   if isinstance(node,(ast.FunctionDef,ast.AsyncFunctionDef,ast.ClassDef)):
    scope=parents+[node.name]
    if isinstance(node,(ast.FunctionDef,ast.AsyncFunctionDef)):
     args=node.args.posonlyargs+node.args.args+node.args.kwonlyargs
     args+=([node.args.vararg] if node.args.vararg else [])+([node.args.kwarg] if node.args.kwarg else [])
     functions.append((node,{'name':'.'.join(scope),'startLine':node.lineno,'endLine':node.end_lineno,'returnAnnotation':ast.unparse(node.returns) if node.returns else None,'parameters':{a.arg:ast.unparse(a.annotation) if a.annotation else None for a in args}}))
   if isinstance(node,(ast.Module,ast.FunctionDef,ast.AsyncFunctionDef,ast.ClassDef)) and node.body:
    first=node.body[0]
    if isinstance(first,ast.Expr) and isinstance(first.value,ast.Constant) and isinstance(first.value.value,str):docs.append((first.lineno,first.end_lineno,'.'.join(scope)))
   if isinstance(node,(ast.Assert,ast.Call,ast.Import,ast.ImportFrom,ast.Assign,ast.AnnAssign)):
    statements.append((node.lineno,node.end_lineno,type(node).__name__,ast.get_source_segment(raw,node)))
   for child in ast.iter_child_nodes(node):visit(child,scope)
  visit(tree,[])
  for packet in code['packets']:
   if packet['file']!=file:continue
   for row in packet['lines']:
    number=row['beforeLine' if side=='before' else 'afterLine']
    if number is None:continue
    if lines[number-1]!=row['text']:raise ValueError(f'Source mismatch {file}:{number}')
    owners=[v for n,v in functions if n.lineno<=number<=n.end_lineno]
    owner=min(owners,key=lambda v:v['endLine']-v['startLine']) if owners else None
    doc=next((d for d in docs if d[0]<=number<=d[1]),None)
    records.append({'id':row['id'],'file':file,'side':side,'line':number,'text':row['text'],
      'function':owner,'docstring':bool(doc),'docstringOwner':doc[2] if doc else None,
      'operations':[{'kind':kind,'startLine':a,'endLine':b,'code':text} for a,b,kind,text in statements if a<=number<=b]})
Path(output).write_text(json.dumps({'baseSha':code['baseSha'],'headSha':code['headSha'],'records':records},ensure_ascii=False,indent=2),encoding='utf-8')
