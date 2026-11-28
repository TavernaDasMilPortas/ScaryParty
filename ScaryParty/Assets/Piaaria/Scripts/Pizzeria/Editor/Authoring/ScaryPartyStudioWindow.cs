using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Visual;
using ScaryParty.Pizzeria.Editor.PizzaVisual;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public class ScaryPartyStudioWindow : OdinMenuEditorWindow
    {
        [MenuItem("Tools/Scary Party/Scary Party Studio", false, 0)]
        public static void Open()
        {
            var w=GetWindow<ScaryPartyStudioWindow>("Scary Party Studio");
            w.minSize=new Vector2(850,600);w.Show();
        }
        protected override OdinMenuTree BuildMenuTree()
        {
            var tree=new OdinMenuTree();
            tree.Config.DrawSearchToolbar=true;
            tree.Add("Início",new Home());
            foreach(var pair in ContentPage.Types)tree.Add("Conteúdo/"+pair.Key,new ContentPage(pair.Value));
            tree.Add("Estúdios/Encaixes e personagens",new AttachmentPage());
            tree.Add("Estúdios/Bancadas e processos",new BenchPage());
            tree.Add("Estúdios/Pizzas",new PizzaPage());
            tree.Add("Estúdios/Pizzaria modular",new ModularPage());
            tree.Add("Configuração/Noites, loja e clientes",new BalancePage());
            tree.Add("Diagnóstico",new DiagnosticsPage());
            return tree;
        }
        public class Home
        {
            [Title("Scary Party Studio","Central de autoria • escolha uma categoria na árvore à esquerda")]
            [InfoBox("Crie, duplique e edite assets por tipo. Registre definições na configuração ativa quando necessário. Os editores anteriores continuam disponíveis.")]
            [Button("Abrir catálogo especializado (Odin)",ButtonSizes.Large)]
            private void Content()=>PizzeriaContentWizard.ShowWindow();
            [Button("Abrir Attachment Studio",ButtonSizes.Large)]
            private void Attach()=>AttachmentStudioWindow.Open();
            [Button("Abrir Estúdio de Bancadas",ButtonSizes.Large)]
            private void Bench()=>PizzeriaContentStudioWindow.ShowWindow();
        }
        public class AttachmentPage
        {
            [Title("Encaixes e rig"), InfoBox("Ajuste objetos sem mover os bones originais. Os perfis de terceira pessoa podem substituir o bone padrão.")]
            [Button("Attachment Studio",ButtonSizes.Large)] private void Attach()=>AttachmentStudioWindow.Open();
            [Button("Configurar personagem")] private void Character()=>ScaryParty.EditorScripts.CharacterSetupWizard.ShowWindow();
            [Button("Mapear armature")] private void Map()=>EditorApplication.ExecuteMenuItem("Tools/Scary Party/Wizards/Personagem & Interação/Armature Mapper");
        }
        public class BenchPage
        {
            [Title("Bancadas e processos"), InfoBox("Prévia de estações, slots, ingredientes e utensílios em uma montagem independente.")]
            [Button("Abrir viewport 3D",ButtonSizes.Large)] private void Open()=>PizzeriaContentStudioWindow.ShowWindow();
        }
        public class PizzaPage
        {
            [Title("Pizza Studio"), InfoBox("Composição visual, distribuição de toppings e comparação de ingredientes.")]
            [Button("Abrir Pizza Studio",ButtonSizes.Large)] private void Open()=>PizzaVisualPreviewWindow.ShowWindow();
        }
        public class ModularPage
        {
            [Title("Pizzaria modular"), InfoBox("Autoria da estrutura física e das estações no prefab. Criar uma definição de bancada não cria automaticamente uma instância física.")]
            [Button("Abrir autoria de prefab",ButtonSizes.Large)] private void Open()=>PizzeriaAuthoringWindow.ShowWindow();
        }
        public class BalancePage
        {
            [Title("Balanceamento"), InfoBox("Use os editores específicos para entradas internas de catálogos, votação e loja.")]
            [Button("Eventos, loja e balanceamento",ButtonSizes.Large)] private void Shop()=>PizzeriaEventsShopBalancingWizard.OpenWindow();
            [Button("Clientes e traços")] private void Clients()=>CustomerTraitWizard.ShowWindow();
        }
        public class DiagnosticsPage
        {
            [Title("Diagnóstico"), InfoBox("Valide a integração da pizzaria e as definições antes do Play Mode.")]
            [Button("Validar setup da pizzaria",ButtonSizes.Large)] private void Validate()=>EditorApplication.ExecuteMenuItem("Tools/Scary Party/Uso Comum/Pizzaria/Validar Setup da Pizzaria");
        }
    }

    public static class StudioAssetPicker
    {
        private static readonly Dictionary<Type,ScriptableObject[]> Cache=new Dictionary<Type,ScriptableObject[]>();
        [InitializeOnLoadMethod] private static void Init(){EditorApplication.projectChanged+=Clear;}
        public static void Clear()=>Cache.Clear();
        public static ScriptableObject[] Find(Type type)
        {
            if(Cache.TryGetValue(type,out var items))return items;
            items=AssetDatabase.FindAssets("t:"+type.Name).Select(AssetDatabase.GUIDToAssetPath)
                .Select(p=>AssetDatabase.LoadAssetAtPath(p,type) as ScriptableObject)
                .Where(a=>a!=null).OrderBy(a=>Label(a)).ToArray();
            Cache[type]=items;return items;
        }
        public static string Label(ScriptableObject asset)
        {
            if(asset==null)return "(Nenhum)";
            var f=NameField(asset.GetType());
            var name=f?.GetValue(asset) as string;
            var id=asset.GetType().GetField("id");
            return (id!=null && id.FieldType==typeof(int)?"#"+id.GetValue(asset)+" • ":"")+
                (string.IsNullOrWhiteSpace(name)?asset.name:name)+" ["+asset.name+"]";
        }
        public static FieldInfo NameField(Type type)
        {
            foreach(var name in new[]{"ingredientName","stationName","toolName","processName","recipeName","layoutName","itemName","upgradeName","displayName","presentationKey"})
            {var f=type.GetField(name);if(f!=null && f.FieldType==typeof(string))return f;}
            return null;
        }
        public static T Draw<T>(GUIContent label,T current) where T:ScriptableObject
        {
            var values=Find(typeof(T)).OfType<T>().ToList();
            if(current!=null && !values.Contains(current))values.Add(current);
            var labels=new[]{"(Nenhum)"}.Concat(values.Select(a=>Label(a))).ToArray();
            int index=EditorGUILayout.Popup(label,current==null?0:values.IndexOf(current)+1,labels.Select(s=>new GUIContent(s)).ToArray());
            return index<=0?null:values[index-1];
        }
    }

    public class ContentPage
    {
        internal static readonly KeyValuePair<string,Type>[] Types={
            Pair<IngredientDef>("Ingredientes"),Pair<ToolDef>("Utensílios"),Pair<StationDef>("Bancadas"),
            Pair<StationSlotLayoutAsset>("Layouts de slots"),Pair<ProcessDef>("Processos"),Pair<RecipeDef>("Receitas"),
            Pair<SupplyItemDef>("Suprimentos"),Pair<UpgradeDef>("Melhorias"),Pair<CustomerTraitDef>("Traços de clientes"),
            Pair<ItemPresentationProfile>("Perfis de apresentação"),Pair<PizzaIngredientVisualProfile>("Perfis de toppings"),
            Pair<PizzeriaRunPreset>("Presets de run"),Pair<NightConfig>("Noites"),Pair<PizzeriaConfig>("Configurações"),
            Pair<RunBalancingCatalog>("Catálogos de balanceamento"),Pair<ItemPresentationCatalog>("Catálogos de apresentação"),
            Pair<PizzaCompositionRules>("Regras de composição"),Pair<PizzeriaAppearanceProfile>("Aparência")
        };
        private static KeyValuePair<string,Type> Pair<T>(string name)=>new KeyValuePair<string,Type>(name,typeof(T));
        private readonly Type type;
        public ContentPage(Type type){this.type=type;}
        [ShowInInspector,ReadOnly,HideLabel,Title("Catálogo por tipo"),DisplayAsString] private string Kind=>type.Name;
        [ShowInInspector,ValueDropdown(nameof(Options)),OnValueChanged(nameof(Select)),Tooltip("Busca todos os assets deste tipo no projeto; não é necessário navegar por pastas.")]
        public ScriptableObject Selected;
        [ShowInInspector,ValueDropdown(nameof(Configs)),Tooltip("Configuração explícita para registrar uma definição. Nenhuma outra configuração é alterada automaticamente.")]
        public PizzeriaConfig TargetConfig;
        [BoxGroup("Criar ou duplicar"),Tooltip("Nome exibido e base do nome do arquivo. IDs numéricos são gerados automaticamente.")]
        public string NewName="Novo conteúdo";
        [BoxGroup("Criar ou duplicar"),ButtonGroup("Criar ou duplicar/Ações"),Button("Criar"),GUIColor(.35f,.85f,.5f)] private void Create()=>CreateAsset(false);
        [ButtonGroup("Criar ou duplicar/Ações"),Button("Duplicar"),EnableIf(nameof(HasSelection))] private void Duplicate()=>CreateAsset(true);
        [ButtonGroup("Operações"),Button("Salvar"),EnableIf(nameof(HasSelection))] private void Save(){if(Selected!=null){EditorUtility.SetDirty(Selected);AssetDatabase.SaveAssets();}}
        [ButtonGroup("Operações"),Button("Localizar"),EnableIf(nameof(HasSelection))] private void Ping(){EditorGUIUtility.PingObject(Selected);}
        [ButtonGroup("Operações"),Button("Excluir"),GUIColor(1f,.5f,.45f),EnableIf(nameof(HasSelection))] private void Delete()
        {
            if(!HasSelection)return;
            var path=AssetDatabase.GetAssetPath(Selected);
            if(!path.StartsWith("Assets/",StringComparison.Ordinal))return;
            if(!EditorUtility.DisplayDialog("Verificar dependências","A exclusão será bloqueada se outro asset em Assets depender deste arquivo. IDs legados em código não podem ser detectados automaticamente.","Verificar","Cancelar"))return;
            var refs=new List<string>();
            var all=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&p!=path&&!AssetDatabase.IsValidFolder(p)&&
                (p.EndsWith(".asset")||p.EndsWith(".prefab")||p.EndsWith(".unity")||p.EndsWith(".controller")||p.EndsWith(".overrideController")||p.EndsWith(".playable"))).ToArray();
            try
            {
                for(int i=0;i<all.Length;i++)
                {
                    if(i%50==0 && EditorUtility.DisplayCancelableProgressBar("Verificando referências",all[i],(float)i/Mathf.Max(1,all.Length)))return;
                    if(AssetDatabase.GetDependencies(all[i],false).Contains(path))refs.Add(all[i]);
                }
            }
            finally{EditorUtility.ClearProgressBar();}
            if(refs.Count>0){EditorUtility.DisplayDialog("Exclusão bloqueada",string.Join("\n",refs.Take(12))+(refs.Count>12?"\n...":"") ,"OK");return;}
            if(!EditorUtility.DisplayDialog("Mover para lixeira?",path+"\nNão há Undo confiável para exclusão de arquivos. Verifique também referências por ID.","Mover para lixeira","Cancelar"))return;
            if(AssetDatabase.MoveAssetToTrash(path)){Selected=null;StudioAssetPicker.Clear();Select();}
        }
        [Button("Registrar definição na configuração selecionada"),ShowIf(nameof(CanRegister)),Tooltip("Adiciona ao catálogo de gameplay selecionado, sem duplicar referências.")]
        private void Register()
        {
            if(!CanRegister)return;
            if(!RegisterIn(TargetConfig,Selected))EditorUtility.DisplayDialog("Catálogo","Já registrado ou existe ID duplicado nesta lista.","OK");
        }
        [Button("Abrir no Attachment Studio"),ShowIf(nameof(IsPresentation))]
        private void Attach()=>AttachmentStudioWindow.OpenFor(Selected as ItemPresentationProfile);
        [ShowInInspector,ReadOnly,DisplayAsString,ShowIf(nameof(HasSelection))] private string Path=>AssetDatabase.GetAssetPath(Selected);
        [ShowInInspector,InlineProperty,HideLabel,ShowIf(nameof(HasSelection))]
        private object Specialized => specialized;
        private object specialized;
        private bool HasSelection=>Selected!=null;
        private bool IsPresentation=>Selected is ItemPresentationProfile;
        private bool CanRegister=>Selected!=null && TargetConfig!=null && ConfigField(type)!=null;
        private ValueDropdownList<ScriptableObject> Options()
        {
            var list=new ValueDropdownList<ScriptableObject>();foreach(var a in StudioAssetPicker.Find(type))list.Add(StudioAssetPicker.Label(a),a);return list;
        }
        private ValueDropdownList<PizzeriaConfig> Configs()
        {
            var list=new ValueDropdownList<PizzeriaConfig>();foreach(var a in StudioAssetPicker.Find(typeof(PizzeriaConfig)).OfType<PizzeriaConfig>())list.Add(a.name,a);return list;
        }
        private void Select()
        {
            if(Selected is IngredientDef i)specialized=new IngredientUnifiedEditor(i);
            else if(Selected is ToolDef t)specialized=new ToolUnifiedEditor(t);
            else if(Selected is StationDef s)specialized=new StationUnifiedEditor(s);
            else if(Selected is StationSlotLayoutAsset l)specialized=new StationUnifiedEditor(l);
            else if(Selected is ProcessDef p)specialized=new ProcessUnifiedEditor(p);
            else if(Selected is RecipeDef r)specialized=new RecipeUnifiedEditor(r);
            else specialized=null;
        }
        [OnInspectorGUI,ShowIf(nameof(HasSelection))]
        private void DrawAsset()
        {
            if(Selected==null)return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Campos completos do asset",EditorStyles.boldLabel);
            var so=new SerializedObject(Selected);so.Update();
            var prop=so.GetIterator();bool enter=true;
            while(prop.NextVisible(enter))
            {
                enter=false;
                if(prop.name=="m_Script"||prop.name=="m_Name")continue;
                using(new EditorGUI.DisabledScope(prop.name=="id"))
                    DrawProperty(prop);
            }
            if (so.ApplyModifiedProperties()) PizzeriaContentStudioSession.NotifySessionChanged();
        }
        private static void DrawProperty(SerializedProperty prop)
        {
            if(prop.propertyType==SerializedPropertyType.ObjectReference)
            {
                var match=System.Text.RegularExpressions.Regex.Match(prop.type,@"PPtr<\$?([^>]+)>");
                var targetType=match.Success?Types.Select(x=>x.Value).FirstOrDefault(t=>t.Name==match.Groups[1].Value):null;
                if(targetType!=null)
                {
                    var assets=StudioAssetPicker.Find(targetType).ToList();
                    var current=prop.objectReferenceValue as ScriptableObject;
                    if(current!=null&&!assets.Contains(current))assets.Add(current);
                    var labels=new[]{"(Nenhum)"}.Concat(assets.Select(StudioAssetPicker.Label)).Select(x=>new GUIContent(x)).ToArray();
                    EditorGUI.BeginChangeCheck();
                    var index=EditorGUILayout.Popup(new GUIContent(prop.displayName,prop.tooltip),current==null?0:assets.IndexOf(current)+1,labels);
                    if(EditorGUI.EndChangeCheck())prop.objectReferenceValue=index==0?null:assets[index-1];
                    return;
                }
            }
            EditorGUILayout.PropertyField(prop,true);
        }
        private void CreateAsset(bool duplicate)
        {
            if(duplicate&&!HasSelection)return;
            var label=(NewName??"").Trim();if(label.Length==0){EditorUtility.DisplayDialog("Nome","Informe um nome.","OK");return;}
            var asset=duplicate?UnityEngine.Object.Instantiate(Selected):ScriptableObject.CreateInstance(type);
            var f=StudioAssetPicker.NameField(type);f?.SetValue(asset,label);
            var id=type.GetField("id");
            if(id!=null&&id.FieldType==typeof(int))
            {
                var max=StudioAssetPicker.Find(type).Select(x=>(int)id.GetValue(x)).DefaultIfEmpty(0).Max();
                if(max==int.MaxValue){UnityEngine.Object.DestroyImmediate(asset);return;}
                id.SetValue(asset,Mathf.Max(1,max+1));
            }
            const string folder="Assets/Piaaria/ScriptableObjects/Pizzeria";
            if(!AssetDatabase.IsValidFolder(folder)){UnityEngine.Object.DestroyImmediate(asset);EditorUtility.DisplayDialog("Pasta","Pasta canônica não encontrada.","OK");return;}
            var safe=string.Concat(label.Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_'));
            string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/"+type.Name+"_"+safe+".asset");
            AssetDatabase.CreateAsset(asset,path);AssetDatabase.SaveAssets();StudioAssetPicker.Clear();Selected=asset;Select();
        }
        private static string ConfigField(Type t)
        {
            if(t==typeof(IngredientDef))return "ingredients";if(t==typeof(ToolDef))return "tools";
            if(t==typeof(StationDef))return "stations";if(t==typeof(ProcessDef))return "processes";
            if(t==typeof(RecipeDef))return "recipes";if(t==typeof(SupplyItemDef))return "supplyItems";
            if(t==typeof(UpgradeDef))return "upgrades";if(t==typeof(CustomerTraitDef))return "customerTraits";
            if(t==typeof(PizzeriaRunPreset))return "availablePresets";return null;
        }
        private static bool RegisterIn(PizzeriaConfig config,ScriptableObject item)
        {
            var so=new SerializedObject(config);var field=ConfigField(item.GetType());if(field==null)return false;
            var list=so.FindProperty(field);if(list==null||!list.isArray)return false;
            var id=item.GetType().GetField("id");
            for(int i=0;i<list.arraySize;i++)
            {
                var existing=list.GetArrayElementAtIndex(i).objectReferenceValue;
                if(existing==item)return false;
                if(existing!=null && id!=null && id.FieldType==typeof(int) && Equals(id.GetValue(existing),id.GetValue(item)))return false;
            }
            Undo.RecordObject(config,"Registrar conteúdo");int n=list.arraySize;list.InsertArrayElementAtIndex(n);
            list.GetArrayElementAtIndex(n).objectReferenceValue=item;so.ApplyModifiedProperties();AssetDatabase.SaveAssets();return true;
        }
    }
}
