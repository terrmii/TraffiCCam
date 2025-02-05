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
using JsonException = System.Text.Json.JsonException;
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
                //Mostrar message por consola
                Console.WriteLine(message);
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
                    else if (message.Contains("deleteUser"))
                    {
                        if (int.TryParse(message.Split('=')[1], out int userId))
                        {
                            EliminarUsuarioDesdeUI(userId);
                        }
                        else
                        {
                            MessageBox.Show("El ID de usuario no es válido.");
                        }
                    }
                    else if (message.Contains("addUser"))
                    {
                        var userData = JsonConvert.DeserializeObject<User>(message);
                        _ = AgregarUsuario(userData.Name, userData.Password);
                    }
                    else if (message.Contains("editUser"))
                    {
                        var messageObject = JsonSerializer.Deserialize<Dictionary<string, object>>(message);
                        var userData = JsonSerializer.Deserialize<User>(messageObject["data"].ToString());
                        ActualizarUsuario(userData.Id, userData.Name, userData.Password);
                    }
                    else if (message.Contains("logout"))
                    {
                        CerrarSesion();
                    }
                    else if (message.Contains("fetchIncidences"))
                    {
                        _ = FetchIncidences();
                    }
                    else if (message.Contains("fetchIncidencesByCity"))
                    {
                        var messageObject = JsonSerializer.Deserialize<Dictionary<string, string>>(message);
                        var city = messageObject["city"];
                        _ = FetchIncidencesByCity(city);
                    }
                    else
                    {
                        MessageBox.Show("Formato de mensaje no válido: " + message);
                    }
                }
            }
            catch (JsonException jsonEx)
            {
                MessageBox.Show("Error al procesar el mensaje JSON: " + jsonEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al procesar el mensaje: " + ex.Message);
            }
        }
        private async Task FetchIncidencesByCity(string city)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync("http://10.10.13.154:8080/incidences");
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        var incidences = JsonSerializer.Deserialize<List<Incidence>>(json);

                        // Filtrar incidencias por ciudad
                        var filteredIncidences = incidences
                            .Where(i => i.CityTown != null && i.CityTown.Equals(city, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        // Limpiar el contenido de la tabla antes de agregar nuevas filas
                        await webView.CoreWebView2.ExecuteScriptAsync("document.getElementById('incidencesContent').innerHTML = '';");

                        if (filteredIncidences.Any())
                        {
                            string tableRows = string.Join("", filteredIncidences.Select(i =>
                                $"<tr>" +
                                $"<td class='py-2 px-4 border-b'>{i.Cause}</td>" +
                                $"<td class='py-2 px-4 border-b'>{i.Latitude}</td>" +
                                $"<td class='py-2 px-4 border-b'>{i.Longitude}</td>" +
                                $"<td class='py-2 px-4 border-b'>{i.CityTown}</td>" +
                                $"<td class='py-2 px-4 border-b'>{i.StartDate}</td>" +
                                $"<td class='py-2 px-4 border-b'>{i.EndDate}</td>" +
                                $"</tr>"
                            ));

                            await webView.CoreWebView2.ExecuteScriptAsync($"document.getElementById('incidencesContent').innerHTML = `{tableRows}`;");
                        }
                        else
                        {
                            await webView.CoreWebView2.ExecuteScriptAsync($"document.getElementById('incidencesContent').innerHTML = '<tr><td colspan=\"6\" class=\"py-2 px-4 border-b text-center\">No hay incidencias disponibles en {city}</td></tr>';");
                        }

                        await webView.CoreWebView2.ExecuteScriptAsync("document.getElementById('incidencesContainer').classList.remove('hidden');");
                    }
                    else
                    {
                        MessageBox.Show("Error al obtener las incidencias");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al obtener las incidencias: " + ex.Message);
            }
        }
        private async Task FetchIncidences()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync("http://10.10.13.154:8080/incidences");
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        var incidences = JsonSerializer.Deserialize<List<Incidence>>(json);

                        var filteredIncidences = incidences.Select(i => new
                        {
                            i.Cause,
                            i.Latitude,
                            i.Longitude,
                            i.CityTown,
                            i.StartDate,
                            i.EndDate
                        }).ToList();

                        // Limpiar el contenido de la tabla antes de agregar nuevas filas
                        await webView.CoreWebView2.ExecuteScriptAsync("document.getElementById('incidencesContent').innerHTML = '';");

                        string tableRows = string.Join("", filteredIncidences.Select(i =>
                            $"<tr>" +
                            $"<td class='py-2 px-4 border-b'>{i.Cause}</td>" +
                            $"<td class='py-2 px-4 border-b'>{i.Latitude}</td>" +
                            $"<td class='py-2 px-4 border-b'>{i.Longitude}</td>" +
                            $"<td class='py-2 px-4 border-b'>{i.CityTown}</td>" +
                            $"<td class='py-2 px-4 border-b'>{i.StartDate}</td>" +
                            $"<td class='py-2 px-4 border-b'>{i.EndDate}</td>" +
                            $"</tr>"
                        ));

                        await webView.CoreWebView2.ExecuteScriptAsync($"document.getElementById('incidencesContent').innerHTML = `{tableRows}`;");
                        await webView.CoreWebView2.ExecuteScriptAsync("document.getElementById('incidencesContainer').classList.remove('hidden');");
                    }
                    else
                    {
                        MessageBox.Show("Error al obtener las incidencias");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al obtener las incidencias: " + ex.Message);
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
                    return usuarios;
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

                    var content = new StringContent(JsonSerializer.Serialize(new { name, password }), System.Text.Encoding.UTF8, "application/json");
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await client.PostAsync("/login", content);

                    if (response.IsSuccessStatusCode)
                    {
                        List<User> users = await LeerUsuarios();

                        if (users != null)
                        {
                            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                            string htmlFilePath = Path.Combine(appDirectory, "html", "inicio.html");

                            if (File.Exists(htmlFilePath))
                            {
                                webView.CoreWebView2.Navigate(new Uri(htmlFilePath).AbsoluteUri);
                                webView.CoreWebView2.NavigationCompleted += async (s, e) =>
                                {
                                    await webView.CoreWebView2.ExecuteScriptAsync($"document.getElementById('username').innerText = '{name}';");

                                    var usersJson = JsonSerializer.Serialize(users);
                                    await webView.CoreWebView2.ExecuteScriptAsync($"updateUsersTable({usersJson});");
                                };
                            }
                            else
                            {
                                MessageBox.Show("El archivo HTML de inicio no se encontró.");
                            }
                        }
                        else
                        {
                            MessageBox.Show("No se encontraron usuarios.", "Lista de Usuarios");
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

        private async Task<bool> EliminarUsuario(int userId)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.DeleteAsync($"http://10.10.13.154:8080/users/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    MessageBox.Show("Error al eliminar el usuario");
                    return false;
                }
            }
        }

        private async void EliminarUsuarioDesdeUI(int userId)
        {
            bool eliminado = await EliminarUsuario(userId);
            if (eliminado)
            {
                List<User> users = await LeerUsuarios();
                if (users != null)
                {
                    var usersJson = JsonSerializer.Serialize(users);
                    await webView.CoreWebView2.ExecuteScriptAsync($"updateUsersTable({usersJson});");
                }
            }
        }

        // Método para actualizar la tabla de usuarios en la interfaz web
        private async void ActualizarTablaUsuarios()
        {
            List<User> users = await LeerUsuarios();
            if (users != null)
            {
                var usersJson = JsonSerializer.Serialize(users);
                await webView.CoreWebView2.ExecuteScriptAsync($"updateUsersTable({usersJson});");
            }
        }

        // Método para manejar el evento de cierre de sesión
        private void CerrarSesion()
        {
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string htmlFilePath = Path.Combine(appDirectory, "html", "login.html");

            if (File.Exists(htmlFilePath))
            {
                webView.CoreWebView2.Navigate(new Uri(htmlFilePath).AbsoluteUri);
            }
            else
            {
                MessageBox.Show("El archivo HTML de inicio de sesión no se encontró.");
            }
        }

        // Método para manejar el evento de actualización de usuario
        private async void ActualizarUsuario(int userId, string newName, string newPassword)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri("http://10.10.13.154:8080");

                    var userData = new
                    {
                        Id = userId,
                        Name = newName,
                        Password = newPassword
                    };

                    var content = new StringContent(JsonSerializer.Serialize(userData), System.Text.Encoding.UTF8, "application/json");
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"/users/{userId}")
                    {
                        Content = content
                    };

                    // Registrar la solicitud
                    string requestContent = await content.ReadAsStringAsync();
                    Console.WriteLine($"Solicitud enviada: {requestContent}");

                    var response = await client.SendAsync(request);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Registrar la respuesta
                    Console.WriteLine($"Respuesta del servidor: {response.StatusCode}\n{responseContent}");

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Usuario actualizado exitosamente.");
                        ActualizarTablaUsuarios();
                    }
                    else
                    {
                        MessageBox.Show($"Error al actualizar el usuario: {response.StatusCode}\n{responseContent}");
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show("Error al enviar la solicitud: " + httpEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al actualizar el usuario: " + ex.Message);
            }
        }


        // Anadir Usuario
        private async Task<bool> AgregarUsuario(string name, string password)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri("http://10.10.13.154:8080");

                    // JSON con la estructura esperada
                    var userData = new
                    {
                        name = name,
                        password = password
                    };

                    string jsonString = JsonSerializer.Serialize(userData);
                    var jsonContent = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync("/users", jsonContent);

                    string responseContent = await response.Content.ReadAsStringAsync(); // Obtener respuesta de la API

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Usuario agregado exitosamente.");
                        ActualizarTablaUsuarios();
                        return true;
                    }
                    else
                    {
                        MessageBox.Show($"Error al agregar usuario: {response.StatusCode}\n{responseContent}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al agregar usuario: " + ex.Message);
                return false;
            }
        }

        public class User
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Password { get; set; }
            public bool Admin { get; set; }
        }

        public class Incidence
        {
            [JsonPropertyName("cause")]
            public string Cause { get; set; }

            [JsonPropertyName("latitude")]
            public double Latitude { get; set; }

            [JsonPropertyName("longitude")]
            public double Longitude { get; set; }

            [JsonPropertyName("cityTown")]
            public string CityTown { get; set; }

            [JsonPropertyName("startDate")]
            public string StartDate { get; set; }

            [JsonPropertyName("endDate")]
            public string EndDate { get; set; }
        }
    }
}