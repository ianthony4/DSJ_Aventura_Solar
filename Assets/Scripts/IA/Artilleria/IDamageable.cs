/// <summary>
/// Contrato mínimo para cualquier objeto que pueda recibir daño (jugador, drones, torretas...).
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount);
}
