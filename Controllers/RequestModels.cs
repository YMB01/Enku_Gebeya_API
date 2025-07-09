using System.ComponentModel.DataAnnotations;

namespace StockManagement.WebUI.Controllers
{
    public class CreateProductRequest
    {
        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100, ErrorMessage = "Product name cannot exceed 100 characters")]
        public string ProductName { get; set; }

        [Required(ErrorMessage = "SKU is required")]
        [StringLength(50, ErrorMessage = "SKU cannot exceed 50 characters")]
        public string SKU { get; set; }

        public string Description { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
        public decimal UnitPrice { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative")]
        public int QTY { get; set; } = 0;

        public int? WarehouseID { get; set; } // Nullable for optional warehouse
        public string Photo { get; set; }
    }
    public class UpdateProductRequest
    {
        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100, ErrorMessage = "Product name cannot exceed 100 characters")]
        public string ProductName { get; set; }

        [Required(ErrorMessage = "SKU is required")]
        [StringLength(50, ErrorMessage = "SKU cannot exceed 50 characters")]
        public string SKU { get; set; }

        public string Description { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
        public decimal UnitPrice { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative")]
        public int? QTY { get; set; }  // Nullable to make it optional

        public int? WarehouseID { get; set; }  // Nullable to make it optional
        public string Photo { get; set; }
    }

    public class CreateWarehouseRequest
    {
        public string WarehouseName { get; set; }
        public string Location { get; set; }
    }

    public class UpdateWarehouseRequest
    {
        public string WarehouseName { get; set; }
        public string Location { get; set; }
    }

    public class UpdateInventoryRequest
    {
        public int ProductID { get; set; }
        public int WarehouseID { get; set; }
        public int Quantity { get; set; }
        public string TransactionType { get; set; }
        public string Remarks { get; set; }
    }
}