using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Player;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public class AttachmentStudioWindow : OdinEditorWindow
    {
        private const string CharacterPath = "Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab";
        private enum Context { RemoteRight, RemoteLeft, FirstPersonRight, FirstPersonLeft }
        [HorizontalGroup("Main", Width = 320), VerticalGroup("Main/Controls"), Title("1. Selecione o conteúdo")]
        [ValueDropdown(nameof(Profiles)), OnValueChanged(nameof(LoadDraft)), Tooltip("Perfil utilizado pelo catálogo de apresentação em gameplay.")]
        public ItemPresentationProfile Profile;
        [VerticalGroup("Main/Controls"), AssetsOnly, OnValueChanged(nameof(Rebuild)), Tooltip("Prefab com CharacterVisualController. O original nunca é modificado.")]
        public GameObject Character;
        [VerticalGroup("Main/Controls"), OnValueChanged(nameof(LoadDraft)), Tooltip("Primeira pessoa usa os sockets da câmera; terceira pessoa permite escolher bones.")]
        [SerializeField] private Context context;
        [VerticalGroup("Main/Controls"), ShowInInspector, ReadOnly, DisplayAsString]
        private string Status => Profile == null ? "Selecione um perfil de apresentação." :
            !IsRemote ? "FP: offsets relativos à câmera, não ao esqueleto." :
            binding.enabled ? "Override de bone ativo. Tail deve ser calibrado." : "Encaixe legado da mão.";
        [VerticalGroup("Main/Controls"), Title("2. Âncora"), ShowIf(nameof(IsRemote)), OnValueChanged(nameof(RefreshPose)), Tooltip("Ative para substituir a mão padrão apenas neste perfil.")]
        [SerializeField] private bool overrideBone;
        [VerticalGroup("Main/Controls"), ShowIf(nameof(ShowBones)), ValueDropdown(nameof(Bones)), OnValueChanged(nameof(ChangeBone)), Tooltip("Caminho relativo ao CharacterVisualController.")]
        [SerializeField] private string bonePath;
        [VerticalGroup("Main/Controls"), ShowIf(nameof(ShowBones)), EnumToggleButtons, OnValueChanged(nameof(RefreshPose))]
        [SerializeField] private AttachmentEndpoint endpoint;
        [VerticalGroup("Main/Controls"), ShowIf(nameof(ShowTail)), OnValueChanged(nameof(RefreshPose)), Tooltip("Tail medido no espaço local do bone. FBX não preserva necessariamente a extremidade Blender.")]
        [SerializeField] private Vector3 tailLocal;
        [VerticalGroup("Main/Controls"), ShowIf(nameof(ShowCustom)), OnValueChanged(nameof(RefreshPose))]
        [SerializeField] private Vector3 customLocal;
        [VerticalGroup("Main/Controls"), ShowIf(nameof(ShowTail)), Button("Sugerir Tail pelo único filho"), Tooltip("Apenas uma estimativa. Bones terminais precisam de calibração manual.")]
        private void SuggestTail()
        {
            var bone = ResolveSelectedBone();
            if (bone == null || bone.childCount != 1)
            {
                EditorUtility.DisplayDialog("Tail", "Não existe um único filho inequívoco. Informe o vetor local medido no Blender.", "OK"); return;
            }
            tailLocal = bone.GetChild(0).localPosition; RefreshPose();
        }
        [VerticalGroup("Main/Controls"), Title("3. Pose do objeto"), OnValueChanged(nameof(RefreshPose)), Tooltip("Metros locais, graus Euler e escala multiplicativa.")]
        [SerializeField] private ItemPoseOffset pose = ItemPoseOffset.Identity;
        [VerticalGroup("Main/Controls"), Button("Resetar offsets"), GUIColor(.8f,.85f,1f)]
        private void ResetOffsets() { pose = ItemPoseOffset.Identity; RefreshPose(); }
        [VerticalGroup("Main/Controls"), Button("Salvar perfil", ButtonSizes.Large), GUIColor(.35f,.85f,.5f), EnableIf(nameof(CanSave))]
        private void SaveProfile()
        {
            if (!CanSave) return;
            Undo.RecordObject(Profile, "Salvar encaixe");
            switch (context)
            {
                case Context.RemoteRight: Profile.remoteRightHandPose = pose; Profile.remoteRightAttachment = CurrentBinding; break;
                case Context.RemoteLeft: Profile.remoteLeftHandPose = pose; Profile.remoteLeftAttachment = CurrentBinding; break;
                case Context.FirstPersonRight: Profile.fpRightHandPose = pose; break;
                case Context.FirstPersonLeft: Profile.fpLeftHandPose = pose; break;
            }
            EditorUtility.SetDirty(Profile); AssetDatabase.SaveAssets();
            PizzeriaContentStudioSession.NotifySessionChanged();
        }
        [VerticalGroup("Main/Controls"), Button("Recarregar do asset"), Tooltip("Descarta o rascunho não salvo.")]
        private void LoadDraft()
        {
            var b = Profile != null && IsRemote ? Profile.RemoteAttachment(IsLeft ? 0 : 1) : default;
            overrideBone=b.enabled; bonePath=b.bonePath; endpoint=b.endpoint; tailLocal=b.tailLocal; customLocal=b.customLocal;
            pose = Profile == null ? ItemPoseOffset.Identity : IsRemote ?
                (IsLeft ? Profile.remoteLeftHandPose : Profile.remoteRightHandPose) :
                (IsLeft ? Profile.fpLeftHandPose : Profile.fpRightHandPose);
            Rebuild();
        }

        [VerticalGroup("Main/Preview"), Title("Viewport • personagem e objeto")]
        [OnValueChanged(nameof(RefreshPose)), Tooltip("Clipe opcional amostrado em uma cópia isolada.")]
        public AnimationClip Clip;
        [VerticalGroup("Main/Preview"), PropertyRange(0,1), OnValueChanged(nameof(RefreshPose))]
        [SerializeField] private float normalizedTime;
        [VerticalGroup("Main/Preview"), ToggleLeft, Tooltip("Mostra origem, extremidade calibrada e ponto de encaixe.")]
        public bool ShowMarkers = true;
        [VerticalGroup("Main/Preview"), ButtonGroup("Main/Preview/Views"), Button("Enquadrar")]
        private void Frame() { FrameBounds(); Repaint(); }
        [ButtonGroup("Main/Preview/Views"), Button("Frente")]
        private void Front() { yaw=180; pitch=0; Repaint(); }
        [ButtonGroup("Main/Preview/Views"), Button("Lado")]
        private void Side() { yaw=90; pitch=0; Repaint(); }
        [VerticalGroup("Main/Preview"), OnInspectorGUI]
        private void DrawViewport()
        {
            var rect = GUILayoutUtility.GetRect(180, 10000, 380, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (preview == null || characterInstance == null) { EditorGUI.HelpBox(rect,"Selecione um personagem e um perfil com prefab visual.",MessageType.Info); return; }
            var e=Event.current;
            int id=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.MouseDown && rect.Contains(e.mousePosition) && e.button<=2){GUIUtility.hotControl=id;e.Use();}
            else if(e.type==EventType.MouseDrag && GUIUtility.hotControl==id)
            {
                if(e.button==2 || e.shift){var q=Quaternion.Euler(pitch,yaw,0); target-= (q*Vector3.right*e.delta.x-q*Vector3.up*e.delta.y)*distance*.002f;}
                else {yaw+=e.delta.x*.6f;pitch=Mathf.Clamp(pitch+e.delta.y*.6f,-85,85);}
                e.Use(); Repaint();
            }
            else if(e.type==EventType.MouseUp && GUIUtility.hotControl==id){GUIUtility.hotControl=0;e.Use();}
            else if(e.type==EventType.ScrollWheel && rect.Contains(e.mousePosition)){distance=Mathf.Clamp(distance*Mathf.Exp(e.delta.y*.07f),.05f,50f);e.Use();Repaint();}
            if(e.type!=EventType.Repaint) return;
            preview.BeginPreview(rect, GUIStyle.none);
            var camera=preview.camera;
            camera.aspect=rect.width/Mathf.Max(1,rect.height);
            if(IsRemote)
            {
                var q=Quaternion.Euler(pitch,yaw,0);camera.transform.SetPositionAndRotation(target-q*Vector3.forward*distance,q);
            }
            else camera.transform.SetPositionAndRotation(fpRoot.position,fpRoot.rotation);
            preview.Render(true);
            GUI.DrawTexture(rect,preview.EndPreview(),ScaleMode.StretchToFill,false);
            if(ShowMarkers && IsRemote)
            {
                var bone=ResolveSelectedBone();
                if(bone!=null){Mark(rect,bone.position,Color.cyan,"Head");Mark(rect,bone.TransformPoint(tailLocal),Color.green,"Tail");Mark(rect,bone.TransformPoint(CurrentBinding.Point+pose.position),Color.yellow,"Grip");}
            }
            GUI.Label(new Rect(rect.x+8,rect.y+8,rect.width-16,22),IsRemote?"Arrastar: orbitar • Shift/MMB: mover • Scroll: zoom":"FP: câmera fixa (posição padrão de gameplay)",EditorStyles.whiteMiniLabel);
        }
        private void Mark(Rect r,Vector3 world,Color color,string label)
        {
            var v=preview.camera.WorldToViewportPoint(world);if(v.z<=0 || v.x<0 || v.x>1 || v.y<0 || v.y>1)return;
            var p=new Vector2(r.x+v.x*r.width,r.y+(1-v.y)*r.height);
            EditorGUI.DrawRect(new Rect(p.x-4,p.y-4,8,8),color);
            GUI.Label(new Rect(p.x+6,p.y-10,70,20),label,EditorStyles.whiteMiniLabel);
        }

        private PreviewRenderUtility preview;
        private GameObject characterInstance;
        private ItemVisualWrapper wrapper;
        private Transform rigRoot,fpRoot,fpHand;
        private Vector3 target;
        private float yaw=180,pitch=10,distance=2;
        private bool IsRemote => context==Context.RemoteLeft || context==Context.RemoteRight;
        private bool IsLeft => context==Context.RemoteLeft || context==Context.FirstPersonLeft;
        private bool ShowBones => IsRemote && overrideBone;
        private bool ShowTail => ShowBones && endpoint==AttachmentEndpoint.Tail;
        private bool ShowCustom => ShowBones && endpoint==AttachmentEndpoint.Custom;
        private bool CanSave => Profile!=null && (!ShowBones || ResolveSelectedBone()!=null) && pose.scale.x>0 && pose.scale.y>0 && pose.scale.z>0;
        private BoneAttachmentBinding binding => CurrentBinding;
        private BoneAttachmentBinding CurrentBinding => new BoneAttachmentBinding{enabled=overrideBone,bonePath=bonePath,endpoint=endpoint,tailLocal=tailLocal,customLocal=customLocal};

        [MenuItem("Tools/Scary Party/Wizards/Personagem & Interação/Attachment Studio",false,71)]
        public static void Open() => OpenFor(null);
        public static void OpenFor(ItemPresentationProfile profile)
        {
            var window=GetWindow<AttachmentStudioWindow>("Attachment Studio");
            window.minSize=new Vector2(780,560);
            if(profile!=null) window.Profile=profile;
            if(window.Character==null) window.Character=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            window.LoadDraft();window.Show();
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            if(Character==null) Character=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            Undo.undoRedoPerformed+=LoadDraft;
            EditorApplication.delayCall+=InitialBuild;
        }
        private void InitialBuild(){if(this!=null && preview==null) Rebuild();}
        protected override void OnDestroy()
        {
            EditorApplication.delayCall-=InitialBuild;
            Undo.undoRedoPerformed-=LoadDraft;
            DisposePreview();base.OnDestroy();
        }
        private void DisposePreview()
        {
            if(preview!=null){preview.Cleanup();preview=null;}
            characterInstance=null;wrapper=null;rigRoot=null;fpRoot=null;fpHand=null;
        }
        private ValueDropdownList<ItemPresentationProfile> Profiles()
        {
            var list=new ValueDropdownList<ItemPresentationProfile>();
            foreach(var g in AssetDatabase.FindAssets("t:ItemPresentationProfile"))
            {
                var a=AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>(AssetDatabase.GUIDToAssetPath(g));
                if(a!=null)list.Add(a.presentationKey+" / "+a.name,a);
            }
            return list;
        }
        private ValueDropdownList<string> Bones()
        {
            var list=new ValueDropdownList<string>();
            if(rigRoot==null)return list;
            foreach(var t in rigRoot.GetComponentsInChildren<Transform>(true))
                if(t!=rigRoot && (wrapper==null || !t.IsChildOf(wrapper.transform)))
                {var p=AnimationUtility.CalculateTransformPath(t,rigRoot);list.Add(p,p);}
            return list;
        }
        private Transform ResolveSelectedBone()=>rigRoot==null || string.IsNullOrEmpty(bonePath)?null:rigRoot.Find(bonePath);
        private void ChangeBone(){tailLocal=Vector3.zero;customLocal=Vector3.zero;RefreshPose();}
        private static void DisableScripts(GameObject go)
        {
            foreach(var b in go.GetComponentsInChildren<MonoBehaviour>(true)) if(b!=null)b.enabled=false;
            foreach(var c in go.GetComponentsInChildren<Collider>(true))c.enabled=false;
        }
        private void Rebuild()
        {
            DisposePreview();
            if(Character==null)return;
            preview=new PreviewRenderUtility();
            preview.cameraFieldOfView=60;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
            preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.12f,.14f,.17f);
            preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(45,35,0);
            preview.lights[1].intensity=.8f;preview.lights[1].transform.rotation=Quaternion.Euler(30,220,0);
            characterInstance=UnityEngine.Object.Instantiate(Character);characterInstance.hideFlags=HideFlags.HideAndDontSave;
            DisableScripts(characterInstance);preview.AddSingleGO(characterInstance);
            var ctrl=characterInstance.GetComponentInChildren<CharacterVisualController>(true);
            rigRoot=ctrl!=null?ctrl.transform:characterInstance.transform;
            var fp=new GameObject("Preview camera socket");fp.hideFlags=HideFlags.HideAndDontSave;preview.AddSingleGO(fp);fpRoot=fp.transform;
            fpHand=new GameObject("Hand").transform;fpHand.SetParent(fpRoot,false);
            fpHand.localPosition=new Vector3(IsLeft?-.32f:.32f,-.25f,.45f);
            if(Profile!=null && Profile.visualPrefab!=null)
            {
                var go=new GameObject("Attachment preview");go.hideFlags=HideFlags.HideAndDontSave;preview.AddSingleGO(go);
                wrapper=go.AddComponent<ItemVisualWrapper>();
                var model=UnityEngine.Object.Instantiate(Profile.visualPrefab);DisableScripts(model);
                wrapper.BindVisual(Profile,model);
            }
            RefreshPose();FrameBounds();
        }
        private void FrameBounds()
        {
            if(characterInstance==null)return;
            var renderers=characterInstance.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0){target=Vector3.up*.5f;distance=2;return;}
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            target=bounds.center;distance=Mathf.Max(.2f,bounds.extents.magnitude*2.8f);
        }
        private void RefreshPose()
        {
            if(characterInstance==null)return;
            if(Clip!=null)Clip.SampleAnimation(characterInstance,normalizedTime*Clip.length);
            foreach(var r in characterInstance.GetComponentsInChildren<Renderer>(true))
                if(wrapper==null || !r.transform.IsChildOf(wrapper.transform))r.enabled=IsRemote;
            if(wrapper!=null)
            {
                var p=pose;Transform socket=fpHand;
                if(IsRemote)
                {
                    var ctrl=rigRoot.GetComponent<CharacterVisualController>();
                    socket=ctrl!=null?(IsLeft?ctrl.LeftHandBone:ctrl.RightHandBone):rigRoot;
                    if(socket==null)socket=rigRoot;
                    var selected=CurrentBinding.Resolve(rigRoot);
                    if(selected!=null){socket=selected;p.position+=CurrentBinding.Point;}
                }
                wrapper.AttachTo(socket,p,0);
            }
            Repaint();
        }
    }
}
