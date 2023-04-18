using EFCore.Model;
using EFCore.Shared.Interfaces;
using EFCore.Shared.Utility;
using Spectre.Console;

namespace EFCore.UI;
internal class Application
{
    private readonly Menu mainMenu = new();
    private readonly ICategoryService categories;
    private readonly IClientService clients;
    private readonly IProductService products;
    private readonly IOrderService orders;
    private bool eventLoop = true;

    public Application(
        ICategoryService categoryService,
        IClientService clientService,
        IProductService productService,
        IOrderService orderService)
    {
        InitMainMenu();
        this.categories = categoryService;
        this.clients = clientService;
        this.products = productService;
        this.orders = orderService;
    }

    #region Menu Init
    public void InitMainMenu()
    {
        var orders = this.mainMenu.AddItem("Orders");
        this.mainMenu.AddItem("New order", this.NewOrder, orders);
        var sfo = this.mainMenu.AddItem("Search for an order", parent: orders);
        this.mainMenu.AddItem("Get all orders by date span", this.GetOrdersByDates, sfo);
        this.mainMenu.AddItem("Get all orders by client id", this.GetOrdersByClientId, sfo);
        this.mainMenu.AddItem("Find all orders by a product id", this.GetOrdersByProductId, sfo);
        this.mainMenu.AddItem("Show order details by order id", this.ShowOrderDetails, orders);
        this.mainMenu.AddItem("Delete an order", this.DeleteOrder, orders);

        var products = this.mainMenu.AddItem("Products");
        this.mainMenu.AddItem("New product", this.NewProduct, products);
        var sfp = this.mainMenu.AddItem("Search for a product", parent: products);
        this.mainMenu.AddItem("Get a product by name or id", this.FindProductByNameOrId, sfp);
        this.mainMenu.AddItem("Get all the products by categories", this.GetAllProductsByCategory, sfp);
        this.mainMenu.AddItem("Edit product", this.EditProduct, products);                                     
        this.mainMenu.AddItem("Delete product", this.DeleteProduct, products);

        var categories = this.mainMenu.AddItem("Categories");
        this.mainMenu.AddItem("New category", this.NewCategory, categories);
        this.mainMenu.AddItem("Show all categories", this.ShowAllCategories, categories);
        this.mainMenu.AddItem("Edit category", this.EditCategory, categories);                                  
        this.mainMenu.AddItem("Delete category", this.DeleteCategory, categories);

        var clients = this.mainMenu.AddItem("Clients");
        this.mainMenu.AddItem("New client", this.NewClient, clients);
        var fc = this.mainMenu.AddItem("Find client", parent: clients);
        this.mainMenu.AddItem("Find client by last name or id", this.FindClientByLastNameOrId, fc);             
        this.mainMenu.AddItem("Find client by email", this.FindClientByEmail, fc);                              
        this.mainMenu.AddItem("Find client by phone number", this.FindClientByPhone, fc);                       
        this.mainMenu.AddItem("Edit client info", this.EditClientInfo, clients);                                
        this.mainMenu.AddItem("Delete client", this.DeleteClient, clients);

        this.mainMenu.AddItem("Exit", () => this.eventLoop = false);
    }

    #endregion

    #region Entry Point

    public void Run()
    {
        while (this.eventLoop)
        {
            try
            {
                this.mainMenu.Show()?.Invoke();
            }
            catch (InvalidDataException er)
            {
                Console.WriteLine(er.Message);
            }
            Console.WriteLine("Press any key to continue");
            _ = Console.ReadKey();
        }
    }

    #endregion

    #region Selection functionality

    private Product? SelectProduct()
    {
        Console.Write("Specify the product's id (or products's name to search for specific one): > ");
        string? productInfo = Console.ReadLine();
        if (string.IsNullOrEmpty(productInfo))
        {
            Console.WriteLine("  ---   No data to search for a product   ---");
            return null;
        }
        if (!int.TryParse(productInfo, out int productId))
        {
            List<Product> products = this.products.GetProductsByName(productInfo!);
            AnsiConsole.Clear();
            if (products.Count < 1)
            {
                Console.WriteLine("No such products found!");
                return null;
            }
            return AnsiConsole.Prompt(
                new SelectionPrompt<Product>()
                    .AddChoices(products)
                    .UseConverter(p => $"{p.Id} {p.Name} {p.Price}")
            );
        }
        return this.products.GetProductById(productId);
    }

    private Client? SelectClient()
    {
        Console.Write("Specify the customer's id (or customer's last name to search for specific one): > ");
        string? clientInfo = Console.ReadLine();
        if (string.IsNullOrEmpty(clientInfo))
        {
            Console.WriteLine("  ---   No data to search for a client   ---");
            return null;
        }
        if (!int.TryParse(clientInfo, out int clientId))
        {
            List<Client> clients = this.clients.GetClientsByLastName(clientInfo!);
            AnsiConsole.Clear();
            if (clients.Count < 1)
            {
                Console.WriteLine("No such clients found!");
                return null;
            }
            return AnsiConsole.Prompt(
                new SelectionPrompt<Client>()
                    .AddChoices(clients)
                    .UseConverter(c => $"{c.Id} {c.LastName} {c.FirstName} {c.Email}")
            );
        }
        return this.clients.GetClientById(clientId);
    }

    private Category? SelectCategory()
    {
        AnsiConsole.Clear();
        Console.Write("Specify the category id (or part of the category name to search for specific one): > ");
        string? catInfo = Console.ReadLine();
        if (string.IsNullOrEmpty(catInfo))
        {
            Console.WriteLine("  ---   No data to search for a category   ---");
            return null;
        }
        if (!int.TryParse(catInfo, out int catId))
        {
            List<Category> categories = this.categories.GetCategoriesByName(catInfo!);
            AnsiConsole.Clear();
            if (categories.Count < 1)
            {
                Console.WriteLine("No such categories found!");
                return null;
            }
            return AnsiConsole.Prompt(
                new SelectionPrompt<Category>()
                    .AddChoices(categories)
                    .UseConverter(c => $"{c.Id} {c.Name}")
            );
        }
        return this.categories.GetCategoryById(catId);
    }

    #endregion

    #region Menu Handlers

    #region Orders Section
    private void NewOrder()
    {
        Console.WriteLine("   --- Making a new order ---");
        Client? client = this.SelectClient() ?? throw new InvalidDataException("Unable to create an order without a client info");
        Console.WriteLine("Add products to the order:");
        var order = new Order()
        {
            IssueDateTime = DateTime.Now,
            Client = client
        };
        do
        {
            Product? current = this.SelectProduct();
            if (current is not null)
            {
                int quantity = AnsiConsole.Prompt(new TextPrompt<int>("Quantity > "));
                order.Items.Add(new OrderItem()
                {
                    Price = current.Price,
                    Product = current,
                    Quantity = quantity
                });
            }
        } while (AnsiConsole.Confirm("Add more products?"));
        if (order.Items.Count < 1)
            throw new InvalidDataException("Unable to make an order without any products in it");
        this.orders.Add(order);
        Console.WriteLine($"Added new order:" +
            $"\nID      : {order.Id}" +
            $"\nDate    : {order.IssueDateTime}" +
            $"\nClient  : {order.Client.LastName} {order.Client.FirstName}");
    }
    private void ShowOrderDetails()
    {
        Console.Write("Input order id: ");
        int orderId;
        if (!int.TryParse(Console.ReadLine(), out orderId))
        {
            Console.WriteLine($"Not a valid id");
            return;
        }
        Order? order = this.orders.FindById(orderId, true);
        if (order is null)
        {
            Console.WriteLine($"Unable to find an order by id [{orderId}]");
            return;
        }
        Console.WriteLine($"Id        : {order.Id}\n" +
                            $"Client    : {order.Client.LastName} {order.Client.FirstName}\n" +
                            $"Issue date: {order.IssueDateTime.Date}");
        if (order.Items.Count < 1)
        {
            Console.WriteLine("No products in the order");
            return;
        }
        foreach (var item in order.Items)
            Console.WriteLine($" > product: [ {item.Product.Name,-60} ] quantity: [ {item.Quantity} ] price: [ {item.Price} ]");
    }
    private void GetOrdersByDates()
    {
        var startDate = ConsoleUtilities.ReadDateTime("Input start date: ");
        var endDate = ConsoleUtilities.ReadDateTime("Input end date: ");
        var result = this.orders.Search(o => o.IssueDateTime.IsBetweenIncluded(startDate, endDate), true);
        Console.WriteLine("All the orders made between specified dates:");
        foreach (Order order in result)
            Console.WriteLine($"Order id: [{order.Id,-4}] By client: [{order.Client.LastName,-12}{order.Client.FirstName,-12}] {order.IssueDateTime}");
    }
    private void GetOrdersByClientId()
    {
        Console.WriteLine("Enter client id:");
        int clientId = int.Parse(Console.ReadLine());

        var orders = sfo.Orders.Where(o => o.Clients.Any(c => c.Id == clientId));
        if (orders.Any())
        {
            foreach (var order in orders)
            {
                Console.WriteLine($"Order id:{order.Id},\n" +
                    $"Order Date:{order.Date}");
                Console.WriteLine("Clients:");
                foreach (var client in order.Clients.Where(c => c.Id == clientId))
                {
                    Console.WriteLine($"{client.Name}");
                }
            }
        }
        else
            Console.WriteLine("Nothing :(");

        //throw new NotImplementedException();
    }
    private void GetOrdersByProductId(SalesOrder sfo)
    {
        Console.WriteLine("Enter product id:");
        int productId = int.Parse(Console.ReadLine());

        var orders = sfo.Orders.Where(o => o.Products.Any(p => p.Id == productId));
        if (orders.Any())
        {
            foreach (var order in orders)
            {
                Console.WriteLine($"Order id:{order.Id},\n" +
                    $"Order Date:{order.Date}");
                Console.WriteLine("Products:");
                foreach (var product in order.Products.Where(p => p.Id == productId))
                {
                    Console.WriteLine($"{product.Name}\t Quantity:{product.Quantity}");
                }
            }
        }
        else
            Console.WriteLine("Nothing :(");

        //throw new NotImplementedException();
    }
    private void DeleteOrder()
    {
        int orderId = 1;
        Order orderDelete = orders.FirstOrDefault(o => o.Id == orderId);

        if (orderDelete == null)
            Console.WriteLine($"Order with [{orderId}] id not found.");
        else
        {
            orders.Remove(orderDelete);
            Console.WriteLine($"Order with [{orderId}] id deleted.");
        }

        //throw new NotImplementedException();
    }
    #endregion

    #region Products Section

    private void NewProduct()
    {
        Console.WriteLine("   --- Making a new product ---");
        var product = new Product()
        {
            Name = AnsiConsole.Prompt(new TextPrompt<string>("Product name: ")),
            Description = AnsiConsole.Prompt(new TextPrompt<string>("Product description: ")),
            Price = AnsiConsole.Prompt(new TextPrompt<decimal>("Product price: "))
        };
        while (AnsiConsole.Confirm("Do you want to add a category to the product?"))
        {
            Category? category = this.SelectCategory();
            if (category is not null)
            {
                product.Categories.Add(category);
                Console.WriteLine($"[{product.Name}] has been assigned the category [{category.Name}]");
            }
        }
        this.products.Add(product);
    }

    private void FindProductByNameOrId()
    {
        Product? product = this.SelectProduct();
        if (product is null)
        {
            Console.WriteLine("No products found");
            return;
        }
        if (product.Categories.Count < 1)
            this.products.LoadCategories(product);
        Console.WriteLine($"Id            : {product.Id}\n" +
                          $"Name          : {product.Name}\n" +
                          $"Description   : {product.Description}\n" +
                          $"Price         : {product.Price}\n" +
                          $"Categories    : {(product.Categories.Count < 1 ? "<NO CATEGORIES>" : string.Join(", ", product.Categories.Select(c => c.Name).ToList()))}");
    }

    private void GetAllProductsByCategory()
    {
        Category? category = this.SelectCategory();
        if (category is null)
        {
            Console.WriteLine("No category found");
            return;
        }
        if (category.Products.Count < 1)
            this.categories.LoadProducts(category);
        foreach (Product product in category.Products)
            Console.WriteLine($"product id: [ {product.Id,-4} ] name: [ {product.Name,-40} ] price: [ {product.Price} ]");
    }

    private void EditProduct(object sender, EventArgs e, List<Product> products)
    {
        Console.WriteLine("Enter product id:");
        int id = int.Parse(Console.ReadLine());

        Product product = products.FirstOrDefault(p => p.Id == id);
        if(product is null)
        {
            Console.WriteLine("There isn't any product with this id.");
            return;
        }
        Console.WriteLine($"Edit product <{product.Name}>:");
        Console.Write("Enter new name:");
        string newName = Console.ReadLine();
        if(!string.IsNullOrEmpty(newName)) { product.Name = newName; }

        Console.WriteLine("Enter new price:");
        string newPrice  = Console.ReadLine();
        if(!string.IsNullOrEmpty(newPrice)) {  product.Price = newPrice; }
        Console.WriteLine("Updating is finished!");

        //throw new NotImplementedException();
    }

    private void DeleteProduct()
    {
        int productId = 1;
        Product productDelete = products.FirstOrDefault(p => p.Id == productId);

        if (productDelete == null)
            Console.WriteLine($"Product with [{productId}] id not found.");
        else
        {
            products.Remove(productDelete);
            Console.WriteLine($"Product with [{productId}] id deleted.");
        }

        //throw new NotImplementedException();
    }

    #endregion

    #region Categories Section

    private void NewCategory()
    {
        Console.WriteLine("   --- Making a new category ---");
        Console.Write("Category name: ");
        string catName = Console.ReadLine();
        this.categories.Add(catName);
    }

    private void ShowAllCategories(object sender, EventArgs e, List<string> categories)
    {
        Console.WriteLine("All categories:");
        foreach (string category in categories)
            Console.WriteLine(category);

        //throw new NotImplementedException();
    }

    private void EditCategory(object sender, EventArgs e, List<Category> categories)
    {
        Console.WriteLine("Enter category id:");
        int id = int.Parse(Console.ReadLine());

        Category category = categories.FirstOrDefault(c => c.Id == id);
        if (category is null)
        {
            Console.WriteLine("There isn't any category with this id.");
            return;
        }
        Console.WriteLine($"Edit category <{category.Name}>:");
        Console.Write("Enter new name:");
        string newName = Console.ReadLine();
        if (!string.IsNullOrEmpty(newName)) { category.Name = newName; }

        Console.WriteLine("Enter new description:");
        string newDescripton = Console.ReadLine();
        if (!string.IsNullOrEmpty(newDescripton)) { category.Description = newDescripton; }
        Console.WriteLine("Updating is finished!");

        //throw new NotImplementedException();
    }

    private void DeleteCategory()
    {
        int categoryId = 1;
        Category categoryDelete = categories.FirstOrDefault(c => c.Id == categoryId);

        if (categoryDelete == null)
            Console.WriteLine($"Category with [{categoryId}] id not found.");
        else
        {
            categories.Remove(categoryDelete);
            Console.WriteLine($"Category with [{categoryId}] id deleted.");
        }

        //throw new NotImplementedException();
    }

    #endregion

    #region Clients Section

    private void NewClient()
    {
        Console.WriteLine("   --- Adding a new client ---");
        Console.Write("Last name: ");
        string? lastName = Console.ReadLine();
        if (string.IsNullOrEmpty(lastName))
            throw new InvalidDataException("Last name can not be empty");
        Console.Write("First name: ");
        string? firstName = Console.ReadLine();
        if (string.IsNullOrEmpty(firstName))
            throw new InvalidDataException("First name can not be empty");
        Console.Write("Email: ");
        string? email = Console.ReadLine();
        if (string.IsNullOrEmpty(email))
            throw new InvalidDataException("Email can not be empty");
        if (this.clients.IsEmailInUse(email))
            throw new InvalidDataException("This email is already in use");
        Console.Write("Phone: ");
        string? phone = Console.ReadLine();
        this.clients.Add(new()
        {
            LastName = lastName!,
            FirstName = firstName!,
            Email = email!,
            Phone = string.IsNullOrEmpty(phone) ? null : phone
        });
    }
    private void DeleteClient()
    {
        int clientId = 1;
        Client clientDelete = clients.FirstOrDefault(c => c.Id == clientId);

        if (clientDelete == null)
            Console.WriteLine($"Client with [{clientId}] id not found.");
        else
        {
            clients.Remove(clientDelete);
            Console.WriteLine($"Client with [{clientId}] id deleted.");
        }

        //throw new NotImplementedException(); 
    }
    private void EditClientInfo() 
    {
        Console.WriteLine("Enter client id:");
        int id = int.Parse(Console.ReadLine());

        Client client = clients.FirstOrDefault(c => c.Id == id);
        if (client is null)
        {
            Console.WriteLine("There isn't any client with this id.");
            return;
        }
        Console.WriteLine($"Edit client <{client.FullName}>:");
        Console.Write("Enter new first name:");
        string newFirstName = Console.ReadLine();
        if (!string.IsNullOrEmpty(newFirstName)) { client.FirstName = newFirstName; }
        
        Console.Write("Enter new last name:");
        string newLastName = Console.ReadLine();
        if (!string.IsNullOrEmpty(newLastName)) { client.LastName = newLastName; }

        Console.Write("Enter new email:");
        string newEmail = Console.ReadLine();
        if (!string.IsNullOrEmpty(newEmail)) { client.Email = newEmail; }

        Console.WriteLine("Enter new phone number:");
        string newPhoneNumber = Console.ReadLine();
        if (!string.IsNullOrEmpty(newPhoneNumber)) { client.PhoneNumber = newPhoneNumber; }
        Console.WriteLine("Updating is finished!");

        //throw new NotImplementedException(); 
    }
    private void FindClientByPhone() 
    {
        Console.WriteLine("Enter phone:");
        string phones = Console.ReadLine();

        List<Client> results = clients.Where(c => c.Phone == phones).ToList();

        if (results.Count == 0)
            Console.WriteLine("There isn't any client with this phone.");
        else
        {
            if (results.Count == 1)
                Console.WriteLine($"There is {results.Count} client:");
            else Console.WriteLine($"There are {results.Count} clients:");
            foreach (Client client in results)
                Console.WriteLine($"Id: {client.Id}\n" +
                    $"Name: {client.FirstName} {client.LastName}.\n");
        }
        //throw new NotImplementedException(); 
    }
    private void FindClientByEmail(object sender, EventArgs e, List<Client> clients)
    {
        Console.WriteLine("Enter email:");
        string emails = Console.ReadLine();

        List<Client> results = clients.Where(c => c.Email == emails).ToList();

        if (results.Count == 0)
            Console.WriteLine("There isn't any client with this email.");
        else
        {
            if (results.Count == 1)
                Console.WriteLine($"There is {results.Count} client:");
            else Console.WriteLine($"There are {results.Count} clients:");
            foreach (Client client in results)
                Console.WriteLine($"Id: {client.Id}\n" +
                    $"Name: {client.FirstName} {client.LastName}.\n");
        }

        //throw new NotImplementedException();
    }
    private void FindClientByLastNameOrId(object sender, EventArgs e, List<Client> clients)
    {
        Console.WriteLine("Enter last name or id:");
        string lastNamesOrIds = Console.ReadLine();

        List<Client> results = clients.Where(c => c.LastName == lastNamesOrIds || c.Id.ToString() == lastNamesOrIds).ToList();

        if (results.Count == 0)
            Console.WriteLine("There isn't any client with this last name or id.");
        else
        {
            if (results.Count == 1)
                Console.WriteLine($"There is {results.Count} client:");
            else Console.WriteLine($"There are {results.Count} clients:");
            foreach (Client client in results)
                Console.WriteLine($"Id: {client.Id}\n" +
                    $"Name: {client.FirstName} {client.LastName}.\n");
        }

        //throw new NotImplementedException();
    }

    #endregion

    #endregion
}
