using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ComicCardVerification
{
    const string Key="ComicCardVerification.Active"; static int stage,frames;static double started;static float next;
    static CardUI[] cards;static CardUI extra;static CardSelectionUI selection;static ComicHoverMotion hover;
    static ComicCardVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
    public static void RunBatch(){EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");new GameObject("Verification GameManager").AddComponent<GameManager>();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);Debug.Log("CARD_CHECK: "+message);}
    static void Capture(string file){typeof(ComicUIVerification).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{file});}
    static T Field<T>(CardUI card,string name) where T:Object=>(T)new SerializedObject(card).FindProperty(name).objectReferenceValue;
    static void Tick(){Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started>100){Debug.LogError("Card test timeout");Finish(1);return;}
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||++frames<30||Time.unscaledTime<next)return;
        try{
            if(stage==0){RoundManager.Instance.BeginRun();Object.FindFirstObjectByType<UIManager>().ShowCardSelectionUI();selection=Object.FindFirstObjectByType<CardSelectionUI>();cards=selection.GetComponentsInChildren<CardUI>();Check(cards.Length==3,"Three live selection slots");stage++;next=Time.unscaledTime+.5f;return;}
            if(stage==1){
                Capture("cards-live.png");
                extra=Object.Instantiate(cards[0],cards[0].transform.parent);cards=cards.Concat(new[]{extra}).ToArray();
                var layout=selection.GetComponent<HorizontalLayoutGroup>();if(layout)layout.enabled=false;
                for(int i=0;i<4;i++){
                    var rect=(RectTransform)cards[i].transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2((i-1.5f)*380,0);rect.sizeDelta=new Vector2(360,507);rect.localScale=Vector3.one;
                    var tile=ScriptableObject.CreateInstance<TileModifierSO>();tile.modifierName="HIZLI BÜYÜME";tile.tileColor=new Color(.3f,.8f,.45f);tile.rarity=(TileRarity)i;tile.modifierRanges.Add(new StatModifierRange{statType=StatType.PlantSpawnRate,target=StatTarget.Planter,operation=ModifierOperation.MorePercent,minValue=-.12f,maxValue=-.12f});
                    var offer=new TileCardOffer(tile);int clicked=0;cards[i].Setup(offer,o=>{Check(ReferenceEquals(o,offer),"Original rolled offer preserved");clicked++;});
                    Check(Field<Image>(cards[i],"cardImage").material.name.Contains(tile.rarity.ToString()),"Rarity material "+tile.rarity);
                    Check(Field<TextMeshProUGUI>(cards[i],"nameText").fontSharedMaterial==Resources.Load<ComicUITheme>("ComicUITheme").outlinedText,"Project toon font material");
                    Check(cards[i].transform.Find("Old Value")==null || !cards[i].transform.Find("Old Value").gameObject.activeSelf,"Old/new comparison hidden");
                    Check(Field<TextMeshProUGUI>(cards[i],"buffText").text==TileBuffText.Amount(offer.Modifiers[0]),"Actual rolled bonus text preserved");
                    Field<Button>(cards[i],"button").onClick.Invoke();Check(clicked==1,"One click callback");
                    Check(cards[i].GetComponentInChildren<ComicCardSparkles>().rarity==tile.rarity,"Rarity FX configured");
                    Check(Field<ComicTilePreview>(cards[i],"tilePreview").color==tile.tileColor,"Preview follows tile color independently of rarity");
                    Check(!Field<ComicTilePreview>(cards[i],"tilePreview").raycastTarget && cards[i].GetComponent<Shadow>().enabled,"Preview permits card clicks and hard card shadow enabled");
                    Check(cards[i].GetComponent<ComicButtonVisual>()==null,"Old palette cannot overwrite rarity");
                }
                hover=cards[2].GetComponent<ComicHoverMotion>();hover.OnPointerEnter(new PointerEventData(EventSystem.current));stage++;next=Time.unscaledTime+.5f;return;
            }
            if(stage==2){Check(Time.timeScale==0&&hover.transform.localScale.x>1,"Paused hover animation");hover.OnPointerExit(new PointerEventData(EventSystem.current));stage++;next=Time.unscaledTime+.4f;return;}
            if(stage==3){Capture("cards-rarities.png");

                var tile=ScriptableObject.CreateInstance<TileModifierSO>();tile.modifierName="MULTI BONUS";tile.modifierRanges.Add(new StatModifierRange{statType=StatType.GoldGainMultiplier,target=StatTarget.All,operation=ModifierOperation.AddPercent,minValue=.2f,maxValue=.2f});tile.modifierRanges.Add(new StatModifierRange{statType=StatType.GoldGainMultiplier,target=StatTarget.All,operation=ModifierOperation.MorePercent,minValue=.1f,maxValue=.1f});
                cards[0].Setup(new TileCardOffer(tile),null);Check(Field<TextMeshProUGUI>(cards[0],"buffText").text.Split('\n').Length==2,"All rolled bonuses remain visible on multi-stat cards");
                Object.DestroyImmediate(extra.gameObject);
                typeof(RoundManager).GetField("pendingCardSelections",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(RoundManager.Instance,1);
                GameManager.Instance.StartCardSelection();selection.RefreshCards();
                var selectedOffer=(TileCardOffer)typeof(CardUI).GetField("currentOffer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(cards[0]);
                Field<Button>(cards[0],"button").onClick.Invoke();
                var applied=Object.FindObjectsByType<GroundCell>(FindObjectsSortMode.None).Where(c=>c.CurrentModifier==selectedOffer.Tile).ToArray();
                Check(applied.Length==1&&applied[0].RolledModifiers.SequenceEqual(selectedOffer.Modifiers),"Actual selection applies exact displayed roll once");
                Check(GameManager.Instance.CurrentState==GameStates.RoundEnd,"Actual selection returns to round preparation");
                Field<Button>(cards[0],"button").onClick.Invoke();Check(Object.FindObjectsByType<GroundCell>(FindObjectsSortMode.None).Count(c=>c.CurrentModifier==selectedOffer.Tile)==1,"Repeated click cannot apply twice");
                Debug.Log("COMIC_CARD_PLAY_PASS");Finish(0);
            }
        }catch(Exception e){Debug.LogException(e);Finish(1);}
    }
    static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
}
