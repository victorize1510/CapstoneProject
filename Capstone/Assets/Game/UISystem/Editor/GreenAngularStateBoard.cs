using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Temporary engine-rendered QA board. Never changes scene/assets/providers.</summary>
    public static class GreenAngularStateBoard {
        public static async Task Capture(VisualElement root){
            var board=new VisualElement{name="green-angular-state-board"};board.AddToClassList("companion-design");board.style.position=Position.Absolute;board.style.width=1600;board.style.height=900;board.style.transformOrigin=new TransformOrigin(0,0,0);float s=Mathf.Min(root.resolvedStyle.width/1600,root.resolvedStyle.height/900);board.style.scale=new Scale(new Vector3(s,s,1));board.style.left=(root.resolvedStyle.width-1600*s)/2;board.style.top=(root.resolvedStyle.height-900*s)/2;board.style.backgroundColor=GreenAngularGeometry.Shell;root.Add(board);
            board.styleSheets.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ThangTest/UIThang/Inventory/UI/CompanionPanel.uss"));board.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
            void Rect(VisualElement e,float x,float y,float w,float h){e.style.position=Position.Absolute;e.style.left=x;e.style.top=y;e.style.width=w;e.style.height=h;}
            string[] states={"Normal","Hover","Pressed","Focus","Selected","Selected + focus","Disabled"};var background=new[]{new Color32(231,232,212,255),new Color32(31,55,46,255),new Color32(142,165,133,255)};
            for(int c=0;c<3;c++){
                for(int y=0;y<900;y+=40)for(int x=0;x<520;x+=40){var tile=new VisualElement{pickingMode=PickingMode.Ignore};Rect(tile,c*530+x,y,40,40);tile.style.backgroundColor=c==2?((x/40+y/40)%2==0?GreenAngularGeometry.Raised:GreenAngularGeometry.Shell):(Color)background[c];board.Add(tile);}
                var title=new Label(c==0?"LIGHT BACKDROP":c==1?"DARK BACKDROP":"DETAILED BACKDROP");Rect(title,c*530+30,20,480,45);title.style.color=c==0?GreenAngularGeometry.Ink:GreenAngularGeometry.Light;title.style.fontSize=24;board.Add(title);
                for(int i=0;i<states.Length;i++){var b=new AngularButton{Shape="parallel",Role="primary",text=states[i]};b.AddToClassList("companion-button");Rect(b,c*530+45,90+i*92,360,64);board.Add(b);b.Surface.Hovered=i==1;b.Surface.Pressed=i==2;b.Surface.HoverAmount=i==1?1:0;b.Surface.PressAmount=i==2?1:0;b.Surface.IsFocused=i==3||i==5;b.Surface.Selected=i==4||i==5;if(i==6)b.SetEnabled(false);b.Surface.MarkDirtyRepaint();
                    var d=new AngularButton{Shape="skill-oct",Role="primary",text="×"};d.AddToClassList("companion-button");Rect(d,c*530+430,90+i*92,64,64);board.Add(d);d.Surface.Hovered=i==1;d.Surface.Pressed=i==2;d.Surface.HoverAmount=i==1?1:0;d.Surface.PressAmount=i==2?1:0;d.Surface.IsFocused=i==3||i==5;d.Surface.Selected=i==4||i==5;if(i==6)d.SetEnabled(false);d.Surface.MarkDirtyRepaint();}
            }
            for(int i=0;i<3;i++){var union=new AngularSurface{Shape="union",ActiveTab=i,Role="panel"};Rect(union,25+i*530,740,768,200);union.style.transformOrigin=new TransformOrigin(0,0,0);union.style.scale=new Scale(new Vector3(.64f,.64f,1));board.Add(union);}
            await Task.Delay(300);ScreenCapture.CaptureScreenshot(GreenAngularVerification.Output+"captures/states-"+Screen.width+"x"+Screen.height+".png");await Task.Delay(350);board.RemoveFromHierarchy();
            // In-engine logical component scale checks, not a claim about Windows display DPI.
            var scales=new VisualElement{name="latest-scale-board"};scales.AddToClassList("companion-design");Rect(scales,board.resolvedStyle.left,board.resolvedStyle.top,1600,900);scales.style.transformOrigin=new TransformOrigin(0,0,0);scales.style.scale=new Scale(new Vector3(s,s,1));scales.style.backgroundColor=GreenAngularGeometry.Shell;scales.styleSheets.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ThangTest/UIThang/Inventory/UI/CompanionPanel.uss"));root.Add(scales);
            for(int j=0;j<3;j++){float k=1+j*.25f,x=20+j*530;var label=new Label("COMPONENT SCALE "+(k*100)+"%");Rect(label,x,25,500,50);scales.Add(label);
                var action=new AngularButton{Shape="parallel",Role="primary",text="Cài đặt / Trở lại"};action.AddToClassList("companion-button");Rect(action,x,100,320,60);action.style.transformOrigin=new TransformOrigin(0,0,0);action.style.scale=new Scale(new Vector3(k,k,1));scales.Add(action);
                var skill=new AngularSurface{Shape="skill-oct",Role="primary",IsFocused=true};Rect(skill,x,230,66,66);skill.style.transformOrigin=new TransformOrigin(0,0,0);skill.style.scale=new Scale(new Vector3(k,k,1));scales.Add(skill);
                var union=new AngularSurface{Shape="union",ActiveTab=j};Rect(union,x,390,864,620);union.style.transformOrigin=new TransformOrigin(0,0,0);union.style.scale=new Scale(new Vector3(.36f*k,.36f*k,1));scales.Add(union);
            }
            await Task.Delay(250);ScreenCapture.CaptureScreenshot(GreenAngularVerification.Output+"captures/component-scales-"+Screen.width+"x"+Screen.height+".png");await Task.Delay(300);scales.RemoveFromHierarchy();
        }
    }
}

