// Round sonu seçimi (ör. boss sonrası uzmanlaşma): kart seçimlerinden sonra, round özetinden önce tamamlanır.
// RoundManager seçimin türünü bilmez; bekleyen varsa RoundChoice durumuna geçer ve bitene kadar sonraki round başlamaz.
public interface IRoundChoice
{
    bool IsPending { get; }
}
