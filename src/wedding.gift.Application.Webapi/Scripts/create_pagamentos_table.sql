CREATE TABLE Pagamentos (
    Id            UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWID(),
    PedidoId      VARCHAR(50)       NOT NULL,
    Metodo        VARCHAR(20)       NOT NULL,
    Valor         DECIMAL(10,2)     NOT NULL,
    Parcelas      INT               NOT NULL DEFAULT 1,
    Status        VARCHAR(30)       NOT NULL,
    NsuInfinite   VARCHAR(100)      NULL,
    BrCodePix     NVARCHAR(MAX)     NULL,
    CriadoEm      DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    AtualizadoEm  DATETIME2         NULL,
    CONSTRAINT PK_Pagamentos PRIMARY KEY (Id)
);

CREATE INDEX IX_Pagamentos_PedidoId ON Pagamentos (PedidoId);
CREATE INDEX IX_Pagamentos_NsuInfinite ON Pagamentos (NsuInfinite);
