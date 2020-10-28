using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Juego_Memorama
{
    public enum Comando { NombreEnviado}
    public class Memorama:INotifyPropertyChanged
    {
        //Propiedades para iniciar partida
        public string Jugador1 { get; set; } = "Jugador";
        public string Jugador2 { get; set; }
        public string IP { get; set; }
        public string Mensaje { get; set; }

        public ICommand IniciarCommand { get; set; }

        HttpListener servidor;
        ClientWebSocket cliente;
        Dispatcher dispatcher;
        WebSocket socket;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool MainWindowVisible { get; set; } = true;
        public byte PuntosJugador1 { get; set; }
        public byte PuntosJugador2 { get; set; }

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


                    string datosRecibidos = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    var comandoRecibido = JsonConvert.DeserializeObject<DatoEnviado>(datosRecibidos);

                    if (cliente != null)
                    {
                        switch (comandoRecibido.Comando)
                        {
                            case Comando.NombreEnviado:
                                Jugador1 = (string)comandoRecibido.Dato;
                                CambiarMensaje("Conectado con el jugador " + Jugador1);
                                break;
                        }
                    }
                    else
                    {
                        switch (comandoRecibido.Comando)
                        {
                            case Comando.NombreEnviado:
                                Jugador2 = (string)comandoRecibido.Dato;
                                CambiarMensaje("Conectado con el jugador " + Jugador2);
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

        lobby VentanaLobby;
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
