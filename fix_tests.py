import re
import sys

def fix_file(filepath):
    with open(filepath, 'r') as f:
        content = f.read()

    # Find where existing Invoices are instantiated and add RowVersion = new byte[8]
    # We look for blocks like new Invoice { ... }
    
    # regex for new Invoice { ... } that doesn't have RowVersion
    # This is simple: just find all new Invoice \n { ... } and insert RowVersion if not present.
    # A quick hack is to just add it before the closing } of the initializer.
    
    def repl(m):
        inner = m.group(1)
        if "RowVersion" not in inner:
            if inner.rstrip().endswith(","):
                inner = inner.rstrip() + "\n            RowVersion = new byte[8]\n        "
            else:
                inner = inner.rstrip() + ",\n            RowVersion = new byte[8]\n        "
        return "new Invoice\n        {" + inner + "}"

    content = re.sub(r'new Invoice\s*\{([^}]*)\}', repl, content, flags=re.MULTILINE)

    with open(filepath, 'w') as f:
        f.write(content)

fix_file('tests/SmallBusiness.Application.Tests/InvoiceServiceTests.cs')
fix_file('tests/SmallBusiness.Application.Tests/PaymentServiceTests.cs')
