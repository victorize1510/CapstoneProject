using System.Collections.Generic;
using Capstone.Game.Inventory;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>32 generic rarity glows, no item identity/collider/raycast. No material creation per drop.</summary>
    public sealed class PveLootView:MonoBehaviour {
        sealed class Glow{public Transform root;public LineRenderer ring,core;public float phase;}
        readonly Dictionary<string,Glow> live=new Dictionary<string,Glow>();readonly Queue<Glow> pool=new Queue<Glow>();
        Transform host;Material material;readonly Dictionary<string,float> receipts=new Dictionary<string,float>();int receiptSequence;
        public int VisibleCount=>live.Count;
        public void Build(Material shared){material=shared;host=new GameObject("PvE generic loot visuals").transform;
            for(int i=0;i<32;i++){
                var root=new GameObject("Loot glow").transform;root.SetParent(host,false);
                var ring=Line(root,"Rarity ring",33,.025f);for(int p=0;p<=32;p++){float a=p/32f*Mathf.PI*2;ring.SetPosition(p,new Vector3(Mathf.Cos(a)*.22f,0,Mathf.Sin(a)*.22f));}
                var core=Line(root,"Unrevealed reward",5,.04f);core.SetPositions(new[]{new Vector3(0,.13f,0),new Vector3(.1f,.3f,0),new Vector3(0,.47f,0),new Vector3(-.1f,.3f,0),new Vector3(0,.13f,0)});
                root.gameObject.SetActive(false);pool.Enqueue(new Glow{root=root,ring=ring,core=core,phase=i*.7f});
            }
        }
        LineRenderer Line(Transform root,string name,int count,float width){var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(root,false);line.useWorldSpace=false;line.sharedMaterial=material;line.positionCount=count;line.widthMultiplier=width;line.numCornerVertices=2;line.numCapVertices=2;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;return line;}
        public void Place(string id,Vector3 position,InventoryItemRarity rarity){
            if(!live.TryGetValue(id,out var glow)){if(pool.Count==0)return;glow=pool.Dequeue();live.Add(id,glow);glow.root.gameObject.SetActive(true);}
            Color color=rarity switch{InventoryItemRarity.Uncommon=>new Color(.42f,1,.6f),InventoryItemRarity.Rare=>new Color(.35f,.72f,1),InventoryItemRarity.Epic=>new Color(.8f,.45f,1),InventoryItemRarity.Legendary=>new Color(1,.87f,.51f),_=>new Color(.85f,.94f,1)};
            glow.root.position=position+Vector3.up*(.04f*Mathf.Sin(Time.unscaledTime*3+glow.phase));glow.root.rotation=Quaternion.Euler(0,Time.unscaledTime*35,0);
            glow.ring.startColor=glow.ring.endColor=color;glow.core.startColor=glow.core.endColor=color;
        }
        public void Remove(string id){if(!live.TryGetValue(id,out var glow))return;live.Remove(id);glow.root.gameObject.SetActive(false);pool.Enqueue(glow);}
        public void Receive(Vector3 position,InventoryItemRarity rarity){string id="received:"+(++receiptSequence);Place(id,position,rarity);receipts[id]=Time.unscaledTime+.45f;}
        void Update(){if(receipts.Count==0)return;string expired=null;foreach(var pair in receipts)if(Time.unscaledTime>=pair.Value){expired=pair.Key;break;}if(expired!=null){receipts.Remove(expired);Remove(expired);}}
        public void Clear(){foreach(var glow in live.Values){glow.root.gameObject.SetActive(false);pool.Enqueue(glow);}live.Clear();receipts.Clear();}
        void OnDestroy(){if(host!=null)Destroy(host.gameObject);}
    }
}
