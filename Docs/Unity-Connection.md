# Resmî Unity bağlantısı

17 Ekim 2026: mevcut Unity CLI 1.0.0-beta.13 ve projeye eklenen
com.unity.pipeline 0.8.0-exp.1 ile bağlantı doğrulandı. Editor 6000.3.14f1
ready / compiling false yanıtı verdi; get_scene_hierarchy GameScene ve
///Managers/SoundManager nesnesini döndürdü. Computer Use kullanılmadı.

Bu proje için komutlar:

```powershell
unity status --project-path 'C:/Users/User/Desktop/Games/Github/ClickerGame' --format json
unity command editor_status --project-path 'C:/Users/User/Desktop/Games/Github/ClickerGame' --format json
unity command get_scene_hierarchy --project-path 'C:/Users/User/Desktop/Games/Github/ClickerGame' --format json
```

`.codex/config.toml` içine proje kapsamlı `mcp_servers.unity` kaydı eklendi.
Bu kayıt Unity CLI'nin `mcp --project-path ...` sunucusunu başlatır. Mevcut
genel ağ/sandbox izinleri değiştirilmedi. MCP araçlarının yüklenmesi için
Codex oturumunun yeniden açılması gerekebilir; CLI mevcut oturumda doğrulandı.
Sandbox Unity'nin yerel bağlantısını veya hesap deposunu engellerse uygun
izinli komutla çalıştır; bunu Editor kapalıymış gibi yorumlama.

Editor açık olmalı. İlk paket yüklemesinde Unity penceresine geçmek importu
başlatır. AI Assistant paketini yüklemek gerekmez. Eski Assistant içindeki
MCP sunucusunun yerine Unity CLI'nin MCP modu öneriliyor:
https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli

Lisanslar: Docs/ThirdParty-Licenses.md ve Docs/Licenses/UnityPipeline/.
