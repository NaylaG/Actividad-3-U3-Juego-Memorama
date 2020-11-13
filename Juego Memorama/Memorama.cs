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
    public class Carta:INotifyPropertyChanged
    {
        public int IdCarta { get; set; }
        public string Imagen { get; set; }
        private bool seleccionada;

        public bool EstaSeleccionada
        {
            get { return seleccionada; }
            set {
                seleccionada = value;
                AlHaberCambio("EstaSeleccionada");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void AlHaberCambio(string propiedad)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
        }
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
        public List<Carta> Adivinadas { get; set; } = new List<Carta>();

        //AGREGASTE ESTO
        public async void ValidarCarta()
        {
            //La carta que seleccionamos se voltea
            CartaSeleccionada.EstaSeleccionada = true;

            var YaAdivinada = 0;
            
            foreach (var item in Adivinadas)
            {   
                //Si la carta ya ha sido adivinada no contara los puntos al clickearla
                if (CartaSeleccionada.IdCarta == item.IdCarta)
                {
                    YaAdivinada++;
                }
                    
            }

            //Aqui entra si no habia adivinado la carta anteriormente
            if(YaAdivinada<1)
            {               
                Hisorial.Add(CartaSeleccionada);

                if (Hisorial.Count == 2)
                {                 
                    var num = 0;
                    Carta[] h = new Carta[2];
                    foreach (var item in Hisorial)
                    {                        
                        h[num] = item;
                        num++;
                    }

                    //Comparamos el ID de las cartas
                    if (h[0].IdCarta == h[1].IdCarta)
                    {
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
                        Adivinadas.Add(h[0]);
                        Adivinadas.Add(h[1]);
                        
                        
                        CambiarMensaje("Cartas iguales");
                        _ = VerificarGanador();
                    }
                    else
                    {

                        h[1].EstaSeleccionada = true;
                        juego.lstTablero.IsEnabled = false;
                        await Task.Delay(1500);
                        juego.lstTablero.IsEnabled = true;
                        h[0].EstaSeleccionada = false;
                        h[1].EstaSeleccionada = false;
                        CambiarMensaje("Vuelve a intentar");                        
                    }
                   
                    Hisorial.Clear();
                }
              
            }
            else
            {
                Hisorial.Clear();
                CambiarMensaje("Carta ya adivinada");
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
            //Creamos un arreglo con los Ids de las cartas
            byte[] cartas = new byte[12] { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6 };

            //Desordenamos el arreglo
            var r = new Random();
            for (int i = 11; i > 0; i--)
            {
                var j = r.Next(0, 12);
                var temp = cartas[i];
                cartas[i] = cartas[j];
                cartas[j] = temp;
            }


            //Instanciamos las cartas por cada posicion del arreglo
            for (int i = 0; i < cartas.Length; i++)
            {
                Carta nueva = new Carta
                {
                    IdCarta = cartas[i],                    
                    Imagen = $"Cartas/{cartas[i]}.jpeg",
                    EstaSeleccionada = false,
                    
                };
                ListaCartas.Add(nueva);
            }
            //Le decimos a la ventana de donde va a sacar los Items
            juego.lstTablero.ItemsSource = ListaCartas;           
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
                                      juego.Title = Jugador2;
                                      juego.DataContext = this;

                                      AsignarCartas();
                                     
                                      juego.ShowDialog();
                                      VentanaLobby.txtNombre.Text = Jugador2;
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
                                    juego.Title = Jugador1;
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

        async Task VerificarGanador()
        {
            if(PuntosJugador1==6&PuntosJugador2==6)
            {
                juego.lstTablero.IsEnabled = false;
                CambiarMensaje($"El juego termino. Ambos jugadores empataron");
                await Task.Delay(3000);
                juego.Close();
                
            }
            if(PuntosJugador1==6||PuntosJugador2==6)
            {
                juego.lstTablero.IsEnabled = false;
                CambiarMensaje($"El juego termino. Gano: {((PuntosJugador1 > PuntosJugador2) ? Jugador1 : Jugador2)}");
                await Task.Delay(3000);
                juego.Close();
 
            }   
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
