using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Capstone.Game.HudSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Capstone.Game.ProfileSystem.Editor {
    public static class ProfileLayoutPreview {
        const string Output="Temp/ProfileRefresh";
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;

        [MenuItem("Game Tools/GameToolThang/HUD/Preview Profile Layout")]
        public static void Preview() {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before rendering the preview.");
            Directory.CreateDirectory(Output);
            var scene=EditorSceneManager.NewPreviewScene();
            var results=new List<string>();
            try {
                var canvasObject=Create(scene,"ProfilePreviewCanvas",typeof(RectTransform),typeof(Canvas));
                var canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace;
                var owner=Create(scene,"ProfilePreviewOwner"); owner.SetActive(false);
                var provider=owner.AddComponent<ProfilePreviewProvider>();
                var team=owner.AddComponent<ProfilePreviewTeam>();
                provider.avatar=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ThangTest/UIThang/GDS/Core.UITK/Styles/Sprites/apple.png");
                foreach(var portrait in Resources.FindObjectsOfTypeAll<Image>())
                    if(portrait.gameObject.scene==SceneManager.GetActiveScene() && portrait.name=="PetPortrait" && portrait.sprite!=null) { team.portrait=portrait.sprite; break; }
                var controller=owner.AddComponent<ProfilePanelController>();
                Set(controller,"targetCanvas",canvas); Set(controller,"providerSource",provider); Set(controller,"teamProviderSource",team);
                controller.RebuildPanel(); controller.Open();
                var root=(RectTransform)canvas.transform.Find("ProfilePanelRoot");
                var design=(RectTransform)root.Find("ProfileScreen");
                Check(design.Find("MainProfilePanel/CurrentTeamSection").childCount>=8,"current team renders six slots",results);
                Check(design.Find("MainProfilePanel/BadgeShowcase")==null,"badge showcase removed from profile",results);
                var occupied=design.Find("MainProfilePanel/CurrentTeamSection/TeamPetSlot_01").GetComponent<Button>();
                occupied.onClick.Invoke();
                Check((int)Get(controller,"selectedTeamIndex")==0 && team.commands==0,"team selection highlights without changing the gameplay pet",results);
                var empty=design.Find("MainProfilePanel/CurrentTeamSection/TeamPetSlot_06").GetComponent<Button>();
                Check(!empty.interactable && empty.transform.Find("EmptyPetIcon").gameObject.activeSelf,"empty team slot remains read-only with empty icon",results);
                Invoke(controller,"ShowNameDialog");
                var input=(InputField)Get(controller,"nameInput"); input.text="Tester";
                Invoke(controller,"ConfirmNameChange");
                Check(provider.name=="Tester" && !((RectTransform)Get(controller,"nameDialog")).gameObject.activeSelf,"rename confirms and refreshes the existing binding",results);
                Invoke(controller,"ChangeAvatar");
                Check(((RectTransform)Get(controller,"avatarDialog")).gameObject.activeSelf,"avatar picker opens",results);
                Invoke(controller,"SelectAvatar",0);
                Check(!((RectTransform)Get(controller,"avatarDialog")).gameObject.activeSelf,"avatar selection closes the picker",results);
                provider.name="ThangVo"; Invoke(controller,"Refresh");
                var cameraObject=Create(scene,"ProfilePreviewCamera",typeof(Camera)); var camera=cameraObject.GetComponent<Camera>();
                camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=2000;camera.transform.position=new Vector3(0,0,-1000);
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.973f,.980f,.953f);
                camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);canvas.worldCamera=camera;
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}) {
                    ((RectTransform)canvas.transform).sizeDelta=size;
                    Canvas.ForceUpdateCanvases();Invoke(controller,"FitDesignSurface");Canvas.ForceUpdateCanvases();
                    foreach(string path in new[]{"MainProfilePanel","PlayerInfoPanel","StatsOverviewPanel"}) {
                        var rect=(RectTransform)design.Find(path);var corners=new Vector3[4];rect.GetWorldCorners(corners);
                        for(int i=0;i<4;i++) {var p=root.InverseTransformPoint(corners[i]);Check(root.rect.Contains(p),size+": "+path+" corner "+i+" is on screen",results);}
                    }
                    foreach(var text in design.GetComponentsInChildren<Text>()) {
                        Check(text.preferredHeight<=text.rectTransform.rect.height+1,size+": text fits "+text.name,results);
                    }
                    camera.orthographicSize=size.y*.5f;camera.aspect=(float)size.x/size.y;
                    var rt=new RenderTexture(size.x,size.y,24);rt.Create();camera.targetTexture=rt;
                    var prior=RenderTexture.active;var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                    try {camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();File.WriteAllBytes(Output+$"/profile-{size.x}x{size.y}.png",texture.EncodeToPNG());}
                    finally{RenderTexture.active=prior;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                }
                // BOX geometry is measured against its actual composed viewport by
                // BoxViewportPreview; stylesheet formatting is not a layout contract.
                File.WriteAllLines(Output+"/validation.txt",results);
                Debug.Log($"Profile layout: {results.Count} PASS. Previews: {Output}. No save data changed.");
            } catch(Exception error) {results.Add("FAIL "+error);File.WriteAllLines(Output+"/validation.txt",results);throw;}
            finally {EditorSceneManager.ClosePreviewScene(scene);}
        }

        [MenuItem("Game Tools/GameToolThang/HUD/Rebuild Profile In Scene")]
        public static void Rebuild() {
            if(Application.isPlaying) return;
            var profile=UnityEngine.Object.FindFirstObjectByType<ProfilePanelController>(FindObjectsInactive.Include);
            if(profile==null) throw new InvalidOperationException("No ProfilePanelController in this scene.");
            var canvas=(Canvas)Get(profile,"targetCanvas");
            if(canvas!=null) Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject,"Rebuild Profile");
            Undo.RecordObject(profile,"Rebuild Profile");
            profile.RebuildPanel();EditorSceneManager.MarkSceneDirty(profile.gameObject.scene);
            Debug.Log("Profile rebuilt. Existing gameplay/save provider references retained.");
        }
        static GameObject Create(Scene scene,string name,params Type[] components){var obj=new GameObject(name,components);SceneManager.MoveGameObjectToScene(obj,scene);return obj;}
        static void Check(bool condition,string text,List<string> results){if(!condition)throw new InvalidOperationException(text);results.Add("PASS "+text);}
        static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Private).SetValue(owner,value);
        static object Get(object owner,string field)=>owner.GetType().GetField(field,Private).GetValue(owner);
        static void Invoke(object owner,string method,params object[] args)=>owner.GetType().GetMethod(method,Private).Invoke(owner,args);
    }

    // Disposable editor-only bindings exercise interactions without touching a save provider.
    public sealed class ProfilePreviewProvider : MonoBehaviour, IPlayerProfileProvider {
        public new string name="ThangVo";
        public Sprite avatar;
        public event Action ProfileChanged;
        public bool CanChangeAvatar=>avatar!=null;
        public IReadOnlyList<Sprite> AvatarOptions=>new[]{avatar};
        public int SelectedAvatarIndex=>0;
        public PlayerProfileSnapshot GetSnapshot()=>new PlayerProfileSnapshot(name,"7A39A426DF82",12,240,600,avatar,6960,"2026-09-28","TestChar",32,8,8,40,1,10,2,10,null,0);
        public bool TrySetDisplayName(string value,out string error){error="";name=value;ProfileChanged?.Invoke();return true;}
        public bool TrySelectAvatar(int index){ProfileChanged?.Invoke();return index==0;}
        public bool TrySelectNextAvatar()=>TrySelectAvatar(0);
    }
    public sealed class ProfilePreviewTeam : MonoBehaviour, IPetHudProvider {
        public Sprite portrait; public int commands;
        public event Action HudDataChanged {add{} remove{}}
        readonly PetSlotHudData[] slots=new PetSlotHudData[6];
        public IReadOnlyList<PetSlotHudData> GetPetSlots(){for(int i=0;i<6;i++)slots[i]=new PetSlotHudData{occupied=i<3,selected=i==1,displayName=i<3?new[]{"Dragon Dusk","Dragon Nightfall","Dragon Darkness"}[i]:"",icon=portrait,level=i+1};return slots;}
        public PetStatusHudData GetSelectedPetStatus()=>default;
        public IReadOnlyList<SkillHudData> GetSkills()=>Array.Empty<SkillHudData>();
        public void SelectPetSlot(int index){commands++;}
        public void RequestSkill(int index){}
    }
}
