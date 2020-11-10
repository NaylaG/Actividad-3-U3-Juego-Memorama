using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Juego_Memorama
{
    public enum Comando { NombreEnviado, PuntajeEnviado}
    //NUEVA CLASE
    public class Carta
    {
        public int IdCarta { get; set; }
        public string Imagen { get; set; }
        public bool Habilitada { get; set; } = true;

    }
    public class Memorama:INotifyPropertyChanged
    {
        //Propiedades para iniciar partida
        public string Jugador1 { get; set; } = "Jugador";
        public string Jugador2 { get; set; }
        public string IP { get; set; } = "localhost";
        public string Mensaje { get; set; }
        //PROPIEDADES NUEVAS
        public List<Carta> ListaCartas { get; set; } = new List<Carta>();
        
        private Carta cartaSeleccionada;
        public Carta CartaSeleccionada
        {
            get { return cartaSeleccionada; }
            set { cartaSeleccionada = value;

                ValidarCarta();
                
            }
        }
        public List<Carta> Hisorial { get; set; } = new List<Carta>();

        //AGREGASTE ESTO
        public void ValidarCarta()
        {
            Hisorial.Add(CartaSeleccionada);
            if(Hisorial.Count==2)
            {
                var num = 0;
                Carta[] h = new Carta[2];
                foreach (var item in Hisorial)
                {                   
                    h[num] = item;
                    num++;
                }
                   
                
                //aqui iria la comparacion
                if(h[0].IdCarta==h[1].IdCarta)
                {                   
                    //Hay que enviar el punteje para que pueda verificar
                    //Hay que aumentar los puntos
                    if (cliente != null)
                    {
                        PuntosJugador2++;
                        EnviarComando(new DatoEnviado { Comando = Comando.PuntajeEnviado, Dato = PuntosJugador2 });
                    }
                    else
                    {
                        PuntosJugador1++;
                        EnviarComando(new DatoEnviado { Comando = Comando.PuntajeEnviado, Dato = PuntosJugador1 });

                    }
                    CambiarMensaje("Cartas iguales");

                    //Hay que inhabilitar las cartas que acertó
                    //cartaSeleccionada.Habilitada = false;
                   
                    _=VerificarGanador();
                }
                else
                {
                    CambiarMensaje("Vuelve a intentar");
                }
                Hisorial.Clear();
            }

        }


        //Ventanas
        VentanaJuego juego;
        lobby VentanaLobby;

        //Comandos
        public ICommand IniciarCommand { get; set; }

        HttpListener servidor;
        ClientWebSocket cliente;
        Dispatcher dispatcher;
        WebSocket socket;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool MainWindowVisible { get; set; } = true;
        public byte PuntosJugador1 { get; set; } = 0;
        public byte PuntosJugador2 { get; set; } = 0;

        //Constructor de la clase
        public Memorama()
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            IniciarCommand = new RelayCommand<bool>(IniciarPartida);
        }
    

        //Servidor crea la partida
        public void CrearPartida()
        {
            servidor = new HttpListener();
            servidor.Prefixes.Add("http://*:1000/ppt/");
            servidor.Start();
            servidor.BeginGetContext(OnContext, null);

            Mensaje = "Esperando que se conecte un adversario...";
            Actualizar();
        }

        
        
        public void AsignarCartas()
        {
            byte[] cartas = new byte[12] { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6 };

            var r = new Random();
            for (int i = 11; i > 0; i--)
            {
                var j = r.Next(0, 12);
                var temp = cartas[i];
                cartas[i] = cartas[j];
                cartas[j] = temp;
            }
            //AGREGASTE ESTO
            //List<Carta> ListaCartas = new List<Carta>();

            for (int i = 0; i < cartas.Length; i++)
            {
                Carta nueva = new Carta
                {
                    IdCarta = cartas[i],
                    Imagen = $"Cartas/{cartas[i]}.jpeg",
                    
                };
                ListaCartas.Add(nueva);
            }

            juego.lstTablero.ItemsSource = ListaCartas;

            //juego.carta1.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}//Cartas//{cartas[0]}.jpeg") as ImageSource;
            //juego.carta2.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[1]}.jpeg") as ImageSource;
            //juego.carta3.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[2]}.jpeg") as ImageSource;
            //juego.carta4.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[3]}.jpeg") as ImageSource;
            //juego.carta5.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[4]}.jpeg") as ImageSource;
            //juego.carta6.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[5]}.jpeg") as ImageSource;
            //juego.carta7.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[6]}.jpeg") as ImageSource;
            //juego.carta8.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[7]}.jpeg") as ImageSource;
            //juego.carta9.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[8]}.jpeg") as ImageSource;
            //juego.carta10.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[9]}.jpeg") as ImageSource;
            //juego.carta11.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[10]}.jpeg") as ImageSource;
            //juego.carta12.Source = new ImageSourceConverter().ConvertFromString($"{AppDomain.CurrentDomain.BaseDirectory}/cartas/{cartas[11]}.jpeg") as ImageSource;
         


            
        }

        private async void OnContext(IAsyncResult ar)
        {
            var context = servidor.EndGetContext(ar);

            if (context.Request.IsWebSocketRequest)
            {
                var listener = await context.AcceptWebSocketAsync(null);
                socket = listener.WebSocket;

                CambiarMensaje("Cliente aceptado. Esperando información del jugador.");

                //Enviar mis datos al cliente
                EnviarComando(new DatoEnviado { Comando = Comando.NombreEnviado, Dato = Jugador1 });

                RecibirComando();
            }
            else
            {

                context.Response.StatusCode = 404;
                context.Response.Close();
                servidor.BeginGetContext(OnContext, null);
            }
        }
        private async void EnviarComando(DatoEnviado datos)
        {
            byte[] buffer;
            var json = JsonConvert.SerializeObject(datos);
            buffer = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        //Cliente se une a la partida
        public async Task ConectarPartida()
        {
            cliente = new ClientWebSocket();
            await cliente.ConnectAsync(new Uri($"ws://{IP}:1000/ppt/"), CancellationToken.None);

            socket = cliente;
            EnviarComando(new DatoEnviado { Comando = Comando.NombreEnviado, Dato = Jugador2 });
            RecibirComando();
        }

        public async void RecibirComando()
        {
            try
            {
                byte[] buffer = new byte[1024];

                while(socket.State==WebSocketState.Open)
                {

                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var datosRecibidos = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var comandoRecibido = JsonConvert.DeserializeObject<DatoEnviado>(datosRecibidos);

                    if (cliente != null)
                    {
                        switch (comandoRecibido.Comando)
                        {
                            case Comando.NombreEnviado:
                                Jugador1 = (string)comandoRecibido.Dato;
                                CambiarMensaje("Conectado con el jugador " + Jugador1);

                                _ = dispatcher.BeginInvoke(new Action(() =>
                                  {
                                      VentanaLobby.Hide();
                                      juego = new VentanaJuego();
                                      juego.Title = "Cliente";
                                      juego.DataContext = this;

                                      AsignarCartas();
                                     
                                      juego.ShowDialog();
                                      VentanaLobby.Show();
                                      
                                  }));
                                break;

                            case Comando.PuntajeEnviado:
                                dispatcher.Invoke(new Action(() =>
                                {
                                    PuntosJugador1 = Convert.ToByte(comandoRecibido.Dato);
                                    CambiarMensaje("El contrincante ha ganado un punto");
                                }));
                                
                                _ = VerificarGanador();
                                break;
                        }
                    }
                    else //Este es servidor
                    {
                        switch (comandoRecibido.Comando)
                        {
                            case Comando.NombreEnviado:
                                Jugador2 = (string)comandoRecibido.Dato;
                                CambiarMensaje("Conectado con el jugador " + Jugador2);
                                _ = dispatcher.BeginInvoke(new Action(() =>
                                {
                                    VentanaLobby.Hide();
                                    juego = new VentanaJuego();
                                    juego.Title = "Servidor";
                                    juego.DataContext = this;

                                    AsignarCartas();
                                    

                                    juego.ShowDialog();
                                    VentanaLobby.Show();
                                    
                                }));
                                break;
                            case Comando.PuntajeEnviado:
                                dispatcher.Invoke(new Action(() =>
                                {
                                    PuntosJugador2 =  Convert.ToByte(comandoRecibido.Dato);
                                    CambiarMensaje("El contrincante ha ganado un punto");
                                }));                               
                               _= VerificarGanador();
                                break;
                        }
                    }
                }
              
            }
            catch(Exception ex)
            {
                CambiarMensaje(ex.Message);
            }
            
        }

        public bool HayGanador { get; set; } = false;
        async Task VerificarGanador()
        {
            if (PuntosJugador1 == 6 && PuntosJugador2 < 6)
            {
                await Task.Delay(1000);
                HayGanador = true;
                CambiarMensaje("Gano el jugador 1");
                
            }
            else if(PuntosJugador2==6&&PuntosJugador1<6)
            {
                await Task.Delay(1000);
                HayGanador = true;
                CambiarMensaje("Gano el jugador 2");               
            }
            if (HayGanador)
                juego.lstTablero.IsEnabled = false;
        }
        private async void IniciarPartida(bool tipoPartida)
        {
            try
            {
                MainWindowVisible = false;
                VentanaLobby = new lobby();
                VentanaLobby.Closing += VentanaLobby_Closing;
                VentanaLobby.DataContext = this;
                VentanaLobby.Show();
                Actualizar();

                if (tipoPartida == true)
                {
                    CrearPartida();
                }
                else
                {
                    Jugador2 = Jugador1;
                    Jugador1 = null;
                    Mensaje = "Intentando conectar con el servidor en " + IP;
                    Actualizar("Mensaje");
                    await ConectarPartida();
                }
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
                Actualizar();
            }

        }

        private void VentanaLobby_Closing(object sender, CancelEventArgs e)
        {
            MainWindowVisible = true;
            Actualizar("MainWindowVisible");

            if (servidor != null)
            {
                servidor.Stop();
                servidor = null;
            }
        }
        void Actualizar(string propertyName = null) //parametro con valor por defecto
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        void CambiarMensaje(string mensaje)
        {
            dispatcher.Invoke(new Action(() =>
            {
                Mensaje = mensaje;
                Actualizar();
            }));
        }
    }

    public class DatoEnviado
    {
        public Comando Comando { get; set; }
        public object Dato { get; set; }
    }

}
