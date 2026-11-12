using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Only active during transitions; paused screens use unscaled time.
public class ComicHoverMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    public float hoverScale=1.055f, tilt=0, duration=.13f;
    // Boşta nefes (11 Kasım 2026, ana menü: "butonlar nefes alabilir"): ölçek bu oranda yavaşça büyüyüp küçülür. 0 = kapalı
    // (oyunun öbür butonları değişmez). Hover geçişi sürerken durur, geçiş bitince kaldığı ölçeğin üstünde devam eder.
    public float breathe=0, breathePeriod=2.8f, breathePhase=0;
    Vector3 restScale;
    Quaternion restRotation;
    Button button;
    bool over,selected,down;
    float targetScale=1;
    Coroutine hoverTween;
    void Update(){
        if(breathe<=0||hoverTween!=null)return;
        float s=1+breathe*Mathf.Sin(Time.unscaledTime*2*Mathf.PI/Mathf.Max(.1f,breathePeriod)+breathePhase);
        transform.localScale=restScale*(targetScale*s);
    }
    public Vector3 RestScale=>restScale;
    public Quaternion RestRotation=>restRotation;
    void Awake(){restScale=transform.localScale;restRotation=transform.localRotation;button=GetComponent<Button>();}
    // UIPop gibi dış animasyonlar bitince hover durumunu yeniden uygular.
    public void Refresh()=>Animate();
    void OnDisable(){if(hoverTween!=null)StopCoroutine(hoverTween);hoverTween=null;over=selected=down=false;targetScale=1;transform.localScale=restScale;transform.localRotation=restRotation;}
    void Animate(){
        if(!isActiveAndEnabled)return;
        bool available=button==null||button.IsInteractable();
        float scale=available?(down?.975f:over||selected?hoverScale:1):1;
        float angle=available&&(over||selected)&&!down?tilt:0;
        targetScale=scale;
        if(hoverTween!=null)StopCoroutine(hoverTween);hoverTween=StartCoroutine(Tween(scale,angle));
    }
    IEnumerator Tween(float scale,float angle){
        Vector3 start=transform.localScale;Quaternion rotation=transform.localRotation,end=restRotation*Quaternion.Euler(0,0,angle);
        for(float t=0;t<duration;t+=Time.unscaledDeltaTime){float k=1-Mathf.Pow(1-Mathf.Clamp01(t/duration),3);transform.localScale=Vector3.LerpUnclamped(start,restScale*scale,k);transform.localRotation=Quaternion.Slerp(rotation,end,k);yield return null;}
        transform.localScale=restScale*scale;transform.localRotation=end;hoverTween=null;
    }
    public void OnPointerEnter(PointerEventData e){over=true;Animate();}
    public void OnPointerExit(PointerEventData e){over=down=false;Animate();}
    public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)down=true;Animate();}
    public void OnPointerUp(PointerEventData e){down=false;Animate();}
    public void OnSelect(BaseEventData e){selected=true;Animate();}
    public void OnDeselect(BaseEventData e){selected=down=false;Animate();}
}
