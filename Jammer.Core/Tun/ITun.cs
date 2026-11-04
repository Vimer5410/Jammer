using System.ComponentModel;

namespace Jammer.Core.Tun;

public interface ITun
{
    
    /// <summary>
    /// Инициализация Tun интерфейса JammerTun
    /// Вызывается первой, нужны права администратора/root
    /// </summary>
    public void InitializeTunnel();


    /// <summary>
    /// Выдача Tun интерфейсу собственного ip адреса
    /// Вызывать после InitializeTunnel маска передается длиной префикса например 24
    /// </summary>
    public void ConfigureIpAddress(string ipAddress, int maskLength);


    /// <summary>
    /// Запуск сессии чтения/записи пакетов
    /// Вызывать после InitializeTunnel без нее SendPacket и ReceivePacket не работают
    /// </summary>
    public void StartSession();


    /// <summary>
    /// Закрытие сессии и самого интерфейса вместе с ним система убирает его адрес и маршруты
    /// Можно вызывать повторно а также если подключение упало на середине
    /// </summary>
    public void CloseSession();



    /// <summary>
    /// Отправка ip пакета в TUN интерфейс
    /// Передается ровно один целый ip пакет, вызывать после StartSession
    /// </summary>
    public void SendPacket(byte[] packet);



    /// <summary>
    /// Чтение одного ip пакета из Tun интерфейса
    /// </summary>
    public byte[] ReceivePacket();
    
    
    
    public static ITun CreateTun()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WinTun();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxTun();
        }

        throw new PlatformNotSupportedException("[ITun] поддержка вашей OS в данный момент не доступна");
    }
}