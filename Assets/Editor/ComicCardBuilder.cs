using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class ComicCardBuilder
{
    const string Root="Assets/Art/UI/ComicToon/";
    static ComicUITheme theme; static Sprite plate; static Material[] palettes;
    public static void RunBatch(){try{
        theme=Resources.Load<ComicUITheme>("ComicUITheme");
        plate=(Sprite)typeof(ComicUIBuilder).GetMethod("Sprite",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"Mutation Card",0});
        palettes=new Material[4];string[] names={"Common","Rare","Epic","Legendary"};
        float[] hues={.56f,.53f,.76f,.12f},saturation={.13f,1.25f,1,1.6f};
        Color[] edges={new Color(.66f,.74f,.77f),new Color(.1f,.8f,1),new Color(.65f,.32f,1),new Color(1,.77f,.08f)};
        Color[] banners={new Color(.48f,.60f,.65f),new Color(.13f,.37f,.5f),new Color(1,.83f,.35f),new Color(.35f,.18f,.07f)};
        for(int i=0;i<4;i++){
            string path=Root+"Card "+names[i]+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("UI/ComicCard"));AssetDatabase.CreateAsset(m,path);}
            m.SetFloat("_BodyHue",hues[i]);m.SetFloat("_BodySaturation",saturation[i]);m.SetColor("_Accent",edges[i]);m.SetColor("_Banner",banners[i]);EditorUtility.SetDirty(m);palettes[i]=m;
        }
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Grid Mutation Card.prefab");Style(prefab.GetComponent<CardUI>());PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/UI/Grid Mutation Card.prefab");PrefabUtility.UnloadPrefabContents(prefab);
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        var selection=Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);var so=new SerializedObject(selection);var slots=so.FindProperty("cardSlots");
        for(int i=0;i<slots.arraySize;i++){
            var card=(CardUI)slots.GetArrayElementAtIndex(i).objectReferenceValue;Style(card);
            var r=(RectTransform)card.transform;r.localScale=Vector3.one;r.sizeDelta=new Vector2(440,620);
            var parent=r.parent;var layout=parent.GetComponent<HorizontalLayoutGroup>();
            if(layout){layout.spacing=36;layout.childControlWidth=false;layout.childControlHeight=false;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;layout.childAlignment=TextAnchor.MiddleCenter;}
            else{r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2((i-(slots.arraySize-1)*.5f)*476,0);}
            Debug.Log("CARD_PARENT: "+parent.name+" / "+((RectTransform)parent).rect);
        }
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("COMIC_CARD_BUILD_PASS");EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    static void Style(CardUI card){
        var so=new SerializedObject(card);var root=(RectTransform)card.transform;
        var legacy=card.GetComponent<ComicButtonVisual>();if(legacy)Object.DestroyImmediate(legacy);
        var image=card.GetComponent<Image>();image.sprite=plate;image.type=Image.Type.Simple;image.material=palettes[0];image.color=Color.white;
        var shadow=card.GetComponent<Shadow>()??card.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.035f,.015f,.06f,.6f);shadow.effectDistance=new Vector2(9,-10);shadow.useGraphicAlpha=true;
        var previewTransform=root.Find("Tile Preview");var preview=previewTransform?previewTransform.GetComponent<ComicTilePreview>():new GameObject("Tile Preview",typeof(RectTransform),typeof(ComicTilePreview)).GetComponent<ComicTilePreview>();
        preview.transform.SetParent(root,false);preview.gameObject.layer=root.gameObject.layer;preview.raycastTarget=false;
        var pr=preview.rectTransform;pr.anchorMin=new Vector2(.13f,.52f);pr.anchorMax=new Vector2(.91f,.82f);pr.offsetMin=pr.offsetMax=Vector2.zero;preview.color=new Color(.4f,.8f,.55f);Set(so,"tilePreview",preview);
        var button=card.GetComponent<Button>();button.transition=Selectable.Transition.None;button.targetGraphic=image;
        var hover=card.GetComponent<ComicHoverMotion>()??card.gameObject.AddComponent<ComicHoverMotion>();hover.hoverScale=1.035f;hover.tilt=-.6f;
        var element=card.GetComponent<LayoutElement>();if(element){element.preferredWidth=440;element.preferredHeight=620;}
        Set(so,"cardImage",image);foreach(var name in new[]{"commonPlate","rarePlate","epicPlate","legendaryPlate"})Set(so,name,plate);
        var mats=so.FindProperty("rarityMaterials");mats.arraySize=4;for(int i=0;i<4;i++)mats.GetArrayElementAtIndex(i).objectReferenceValue=palettes[i];
        var nameText=(TextMeshProUGUI)so.FindProperty("nameText").objectReferenceValue;
        Format(nameText,.1f,.835f,.91f,.965f,36);nameText.rectTransform.localRotation=Quaternion.Euler(0,0,3);
        var rarity=(TextMeshProUGUI)so.FindProperty("rarityText").objectReferenceValue;Format(rarity,.14f,.02f,.91f,.085f,30);
        var buff=(TextMeshProUGUI)so.FindProperty("buffText").objectReferenceValue;Format(buff,.12f,.105f,.9f,.245f,46);
        Set(so,"effectNameText",Label(root,"Effect Name","ETKİ",.12f,.415f,.89f,.48f,24));
        Label(root,"Bonus Caption","TILE ETKİSİ",.15f,.28f,.88f,.37f,25).color=new Color(.16f,.08f,.24f);
        foreach(string obsolete in new[]{"Comparison Caption","Old Value","New Value","Comparison Arrow"}){var child=root.Find(obsolete);if(child)child.gameObject.SetActive(false);}
        var existing=root.Find("Rarity Glints");var fx=existing?existing.GetComponent<ComicCardSparkles>():new GameObject("Rarity Glints",typeof(RectTransform),typeof(ComicCardSparkles)).GetComponent<ComicCardSparkles>();
        fx.transform.SetParent(root,false);var fr=fx.rectTransform;fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;fx.color=new Color(1,.94f,.65f);fx.raycastTarget=false;Set(so,"sparkles",fx);so.ApplyModifiedPropertiesWithoutUndo();
    }
    static TextMeshProUGUI Label(RectTransform root,string name,string caption,float x0,float y0,float x1,float y1,float size){var t=root.Find(name);var text=t?t.GetComponent<TextMeshProUGUI>():new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();text.transform.SetParent(root,false);text.text=caption;Format(text,x0,y0,x1,y1,size);return text;}
    static void Format(TextMeshProUGUI text,float x0,float y0,float x1,float y1,float size){
        var r=text.rectTransform;r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;
        text.font=theme.headingFont;text.fontSharedMaterial=theme.outlinedText;text.color=Color.white;text.alignment=TextAlignmentOptions.Center;text.enableAutoSizing=true;text.fontSizeMin=size*.65f;text.fontSizeMax=size;text.fontSize=size;text.margin=Vector4.zero;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;
    }
    static void Set(SerializedObject so,string name,Object value){so.FindProperty(name).objectReferenceValue=value;}
}
