using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using static TraffiCCam.Form1;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TraffiCCam
{
    public partial class Form1 : Form
    {
        private WebView2 webView;
        private bool isWebViewReady = false;

        public Form1()
        {
            InitializeComponent();

            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            this.Controls.Add(webView);

            // Inicializa el WebView2 y maneja eventos
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                isWebViewReady = true;

                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string htmlFilePath = Path.Combine(appDirectory, "html", "index.html");

                if (File.Exists(htmlFilePath))
                {
                    // Desactiva la caché del WebView2
                    webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                    webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Navigate(new Uri(htmlFilePath).AbsoluteUri);
                }
                else
                {
                    MessageBox.Show("El archivo HTML no se encontró: " + htmlFilePath);
                }

                webView.WebMessageReceived += WebView_WebMessageReceived;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al inicializar WebView2: " + ex.Message);
            }
        }

        private void WebView_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                if (!isWebViewReady)
                {
                    MessageBox.Show("WebView aún no está listo. Intenta nuevamente.");
                    return;
                }

                var message = e.TryGetWebMessageAsString();
                if (!string.IsNullOrEmpty(message))
                {
                    if (message.Contains("username") && message.Contains("password"))
                    {
                        var pairs = message.Split('&');
                        var username = pairs[0].Split('=')[1];
                        var password = pairs[1].Split('=')[1];

                        // Llama al método de inicio de sesión
                        IniciarSesion(username, password);
                    }
                    else
                    {
                        MessageBox.Show("Formato de mensaje no válido: " + message);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al procesar el mensaje: " + ex.Message);
            }
        }

        private async Task<List<User>> LeerUsuarios()
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync("http://10.10.13.154:8080/users");
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    List<User> usuarios = JsonConvert.DeserializeObject<List<User>>(json);

                    return usuarios
                }
                else
                {
                    MessageBox.Show("Error al cargar los usuarios");
                    return null;
                }
            }
        }

        private async void IniciarSesion(string name, string password)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("http://10.10.13.154:8080");

                    // Crea el contenido del POST con el formato correcto
                    var content = new StringContent(JsonSerializer.Serialize(new { name, password }), System.Text.Encoding.UTF8, "application/json");

                    // Añade los encabezados necesarios
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    // Llama a la API para iniciar sesión
                    var response = await client.PostAsync("/login", content);

                    if (response.IsSuccessStatusCode)
                    {
                        // Redirige al HTML de inicio si las credenciales son correctas
                        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                        string htmlFilePath = Path.Combine(appDirectory, "html", "inicio.html");

                        if (File.Exists(htmlFilePath))
                        {
                            webView.CoreWebView2.Navigate(new Uri(htmlFilePath).AbsoluteUri);
                            webView.CoreWebView2.NavigationCompleted += async (s, e) =>
                            {
                                // Enviar el nombre de usuario al HTML
                                await webView.CoreWebView2.ExecuteScriptAsync($"document.getElementById('username').innerText = '{name}';");

                                // Obtener la lista de usuarios y enviarla al HTML
                                List<User> users = await LeerUsuarios();

                                //Pasarl users a JSON
                                string jsonUsers = JsonSerializer.Serialize(users);

                                // Enviar la lista de usuarios al HTML
                                await webView.CoreWebView2.ExecuteScriptAsync($"document.getElementById('users').innerText = '{jsonUsers}';");

                                // Enviar mensaje de éxito al HTML
                                await webView.CoreWebView2.ExecuteScriptAsync("document.getElementById('message').innerText = 'Inicio de sesión exitoso';");

                                // Enviar mensaje de error al HTML
                                await webView.CoreWebView2.ExecuteScriptAsync("document.getElementById('error').innerText = '';");


                            };
                        }
                        else
                        {
                            MessageBox.Show("El archivo HTML de inicio no se encontró.");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Inicio de sesión fallido. Verifique sus credenciales.");
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show("Error al enviar la solicitud: " + httpEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al iniciar sesión: " + ex.Message);
            }
        }
        public class User
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Password { get; set; }
            public bool Admin { get; set; }
        }
    }
}